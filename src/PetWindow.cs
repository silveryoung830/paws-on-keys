using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;
using Forms = System.Windows.Forms;

namespace PawsOnKeys
{
    internal sealed class PetWindow : Window
    {
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);

        private readonly PetSettings settings;
        private readonly PetView pet;
        private readonly Forms.NotifyIcon tray;
        private readonly Forms.ContextMenuStrip menu;
        private readonly System.Drawing.Icon trayIcon;
        private readonly DispatcherTimer timer;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private DesktopInput input;
        private HeadInputWindow headInput;
        private CatEditor editor;
        private double lastTick, mousePulseUntil;
        private bool shuttingDown;
        private IntPtr handle;

        internal PetWindow(PetSettings initial)
        {
            settings = initial;
            Title = "Paws on Keys · 桌角猫";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowActivated = false;
            ShowInTaskbar = false;
            Focusable = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            SizePet();
            pet = new PetView { Look = settings.Cat };
            Content = pet;

            menu = new Forms.ContextMenuStrip();
            menu.Opening += delegate(object sender, CancelEventArgs args) { FillMenu(); args.Cancel = false; };
            trayIcon = TrayArtwork.MakeIcon();
            tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "桌角猫 · 双击设置，右键菜单", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += delegate { OpenEditor(); };
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            timer.Tick += OnTick;
            SourceInitialized += Attach;
            IsVisibleChanged += delegate { if (!shuttingDown) ApplyState(); };
            Closed += OnClosed;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
        }

        private void SizePet() { Width = settings.Width; Height = Width * 0.9; }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (handle != IntPtr.Zero) PositionPet();
        }

        private void Attach(object sender, EventArgs args)
        {
            handle = new WindowInteropHelper(this).Handle;
            SetInputStyle(handle, true);
            PositionPet();
            input = new DesktopInput(HwndSource.FromHwnd(handle), OnKey, delegate { mousePulseUntil = clock.Elapsed.TotalSeconds + .12; });
            ApplyState();
        }

        internal static void SetInputStyle(IntPtr window, bool clickThrough)
        {
            const int exStyle = -20, transparent = 0x20, toolWindow = 0x80, noActivate = 0x08000000;
            int style = GetWindowLong(window, exStyle) | toolWindow | noActivate;
            SetWindowLong(window, exStyle, clickThrough ? style | transparent : style & ~transparent);
        }

        private void DisplayChanged(object sender, EventArgs args)
        {
            if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(new Action(PositionPet));
        }

        private Forms.Screen ActiveScreen()
        {
            foreach (Forms.Screen screen in Forms.Screen.AllScreens)
                if (screen.DeviceName == settings.ScreenName) return screen;
            return Forms.Screen.PrimaryScreen;
        }

        private void PositionPet()
        {
            if (handle == IntPtr.Zero) return;
            var screen = ActiveScreen();
            if (screen == null) return;
            var source = HwndSource.FromHwnd(handle);
            if (source == null || source.CompositionTarget == null) return;
            Matrix scale = source.CompositionTarget.TransformToDevice;
            int w = (int)Math.Ceiling(Width * scale.M11), h = (int)Math.Ceiling(Height * scale.M22);
            var rect = screen.WorkingArea;
            int gap = (int)Math.Round(12 * scale.M11);
            int x = settings.Corner.EndsWith("L") ? rect.Left + gap : rect.Right - w - gap;
            int y = settings.Corner.StartsWith("T") ? rect.Top + gap : rect.Bottom - h - gap;
            SetWindowPos(handle, IntPtr.Zero, Math.Max(rect.Left, x), Math.Max(rect.Top, y), 0, 0, 0x15);
            if (headInput != null) headInput.SyncBounds();
        }

        private void OnKey(int vk) { pet.PressKey(vk, settings.ShowKeys ? DesktopInput.KeyLabel(vk) : ""); }

        private void OnTick(object sender, EventArgs args)
        {
            double now = clock.Elapsed.TotalSeconds;
            DesktopInput.ScreenPoint pointer;
            if (DesktopInput.GetCursorPos(out pointer))
            {
                var screen = Forms.Screen.FromPoint(new System.Drawing.Point(pointer.X, pointer.Y)).Bounds;
                double x = 2.0 * (pointer.X - screen.Left) / Math.Max(1, screen.Width - 1) - 1.0;
                double y = 2.0 * (pointer.Y - screen.Top) / Math.Max(1, screen.Height - 1) - 1.0;
                Point local = pet.PointFromScreen(new Point(pointer.X, pointer.Y));
                bool press = now < mousePulseUntil || DesktopInput.GetAsyncKeyState(1) < 0 || DesktopInput.GetAsyncKeyState(2) < 0;
                pet.SetPointer(x, y, press, local);
            }
            pet.Tick(Math.Min(.1, Math.Max(0, now - lastTick)));
            if (headInput != null) headInput.RefreshHead();
            lastTick = now;
        }

        private void ApplyState()
        {
            if (input == null) return;
            bool run = !settings.Paused && editor == null && IsVisible;
            try { input.Enable(run); }
            catch (Win32Exception ex)
            {
                settings.Paused = true;
                run = false;
                ShowTip("键鼠联动暂停", ex.Message);
            }
            if (run)
            {
                if (headInput == null) headInput = new HeadInputWindow(this, pet);
                headInput.ShowForPet();
                lastTick = clock.Elapsed.TotalSeconds;
                timer.Start();
            }
            else
            {
                if (headInput != null && headInput.IsVisible) headInput.Hide();
                timer.Stop();
                pet.ClearInput();
                mousePulseUntil = 0;
            }
        }

        internal void OpenEditor()
        {
            if (shuttingDown) return;
            if (editor != null) { editor.Activate(); return; }
            editor = new CatEditor(settings.Cat.Copy());
            ApplyState();
            try
            {
                if (editor.ShowDialog() == true)
                {
                    settings.Cat = editor.Result;
                    settings.Cat.Normalize();
                    pet.Look = settings.Cat;
                    Save();
                }
            }
            finally
            {
                editor = null;
                if (!shuttingDown) ApplyState();
            }
        }

        private static Forms.ToolStripMenuItem Item(string label, bool check, Action action)
        {
            var item = new Forms.ToolStripMenuItem(label) { Checked = check };
            item.Click += delegate { action(); };
            return item;
        }

        private void FillMenu()
        {
            // Disposing the old items also releases submenus and their captured delegates.
            while (menu.Items.Count > 0) menu.Items[0].Dispose();
            menu.Items.Add(Item("我的猫…", false, OpenEditor));
            menu.Items.Add(Item("显示上浮键名", settings.ShowKeys, delegate { settings.ShowKeys = !settings.ShowKeys; pet.ClearInput(); Save(); }));
            menu.Items.Add(Item("暂停键鼠联动", settings.Paused, delegate { settings.Paused = !settings.Paused; ApplyState(); Save(); }));
            menu.Items.Add(Item("显示桌宠", IsVisible, delegate { if (IsVisible) Hide(); else { Show(); PositionPet(); } ApplyState(); }));
            var position = new Forms.ToolStripMenuItem("桌宠位置");
            string[] codes = { "TL", "TR", "BL", "BR" }, names = { "左上角", "右上角", "左下角", "右下角" };
            for (int i = 0; i < codes.Length; i++)
            {
                string corner = codes[i];
                position.DropDownItems.Add(Item(names[i], settings.Corner == corner, delegate { settings.Corner = corner; PositionPet(); Save(); }));
            }
            if (Forms.Screen.AllScreens.Length > 1)
            {
                position.DropDownItems.Add(new Forms.ToolStripSeparator());
                foreach (var screen in Forms.Screen.AllScreens)
                {
                    string device = screen.DeviceName;
                    position.DropDownItems.Add(Item(device, ActiveScreen().DeviceName == device, delegate { settings.ScreenName = device; PositionPet(); Save(); }));
                }
            }
            menu.Items.Add(position);
            var sizes = new Forms.ToolStripMenuItem("桌宠大小");
            foreach (int candidate in new int[] { 240, 310, 410 })
            {
                int size = candidate;
                sizes.DropDownItems.Add(Item(size.ToString(), settings.Width == size, delegate { settings.Width = size; SizePet(); PositionPet(); Save(); }));
            }
            menu.Items.Add(sizes);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(Item("退出", false, Close));
        }

        internal void ShowTip(string title, string message) { tray.ShowBalloonTip(3500, title, message, Forms.ToolTipIcon.Info); }

        private void Save()
        {
            try { settings.Save(PetSettings.DefaultPath); }
            catch (IOException ex) { ShowTip("设置仅在本次运行生效", ex.Message); }
            catch (UnauthorizedAccessException ex) { ShowTip("设置仅在本次运行生效", ex.Message); }
        }

        private void OnClosed(object sender, EventArgs args)
        {
            shuttingDown = true;
            if (editor != null) editor.Close();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
            timer.Stop();
            try { if (input != null) input.Dispose(); }
            finally
            {
                tray.Visible = false;
                tray.Dispose();
                menu.Dispose();
                trayIcon.Dispose();
            }
        }
    }
}
