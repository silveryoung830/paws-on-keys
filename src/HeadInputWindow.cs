using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PawsOnKeys
{
    // Windows hit-tests a layered window by pixel alpha. Keep the pet itself
    // click-through and give just the painted face a separate input surface.
    internal sealed class HeadInputWindow : Window
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);

        private sealed class HeadSurface : FrameworkElement
        {
            private readonly PetView pet;
            private static readonly Brush hitBrush = MakeHitBrush();

            internal HeadSurface(PetView pet) { this.pet = pet; }

            private static Brush MakeHitBrush()
            {
                var brush = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                brush.Freeze();
                return brush;
            }

            protected override void OnRender(DrawingContext drawing)
            {
                drawing.DrawGeometry(hitBrush, null, pet.HeadHitGeometry);
            }
        }

        private readonly HeadSurface surface;
        private readonly PetView pet;
        private IntPtr handle;

        internal HeadInputWindow(PetWindow owner, PetView pet)
        {
            this.pet = pet;
            Owner = owner;
            Title = "桌角猫 · 摸头";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width = owner.Width;
            Height = owner.Height;
            surface = new HeadSurface(pet);
            Content = surface;
            SourceInitialized += delegate
            {
                handle = new WindowInteropHelper(this).Handle;
                PetWindow.SetInputStyle(handle, false);
                HwndSource.FromHwnd(handle).AddHook(HandleMouseMessage);
            };
        }

        private IntPtr HandleMouseMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int mouseActivate = 0x0021, noActivate = 3, leftDown = 0x0201, leftDoubleClick = 0x0203;
            if (message == mouseActivate)
            {
                handled = true;
                return new IntPtr(noActivate);
            }
            if (message == leftDown || message == leftDoubleClick)
            {
                pet.PetHead();
                handled = true;
            }
            return IntPtr.Zero;
        }

        internal void ShowForPet()
        {
            if (IsVisible) return;
            new WindowInteropHelper(this).EnsureHandle();
            SyncBounds();
            RefreshHead();
            Show();
        }

        internal void SyncBounds()
        {
            NativeRect bounds;
            if (handle != IntPtr.Zero && GetWindowRect(new WindowInteropHelper(Owner).Handle, out bounds))
                PetWindow.SetWindowPos(handle, IntPtr.Zero, bounds.Left, bounds.Top,
                    bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, 0x14); // preserve z-order and focus
        }

        internal void RefreshHead() { surface.InvalidateVisual(); }
    }
}
