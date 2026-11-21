using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PawsOnKeys
{
    internal static class SmokeTests
    {
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            Console.WriteLine("PASS " + name);
        }

        [STAThread]
        public static int Main(string[] args)
        {
            string temp = Path.Combine(Path.GetTempPath(), "pawtest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                TestSettings(temp);
                TestInput();
                TestTypingRhythm();
                TestTypingPoses();
                TestPhoto(temp);
                Check(DesktopInput.KeyLabel(0x53) == "S" && DesktopInput.KeyLabel(0x1b) == "Esc"
                    && DesktopInput.KeyLabel(0xa3) == "Ctrl", "physical key names");
                var pet = TestArt();
                TestEditorAndWindow();
                if (args.Length == 2 && args[0] == "--preview")
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(Render(pet)));
                    using (var stream = File.Create(args[1])) encoder.Save(stream);
                }
                Console.WriteLine("All smoke tests passed.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
            finally
            {
                // Only the known files created by this test are removed.
                foreach (string name in new[] { "pet.xml", "cat.png" }) File.Delete(Path.Combine(temp, name));
                Directory.Delete(temp);
            }
        }

        private static void TestInput()
        {
            int hits = 0;
            var keys = new List<int>();
            var parameters = new HwndSourceParameters("Paw test packets") { WindowStyle = 0, Width = 2, Height = 2 };
            using (var window = new HwndSource(parameters))
            using (var input = new DesktopInput(window, keys.Add, delegate { hits++; }))
            {
                // No dispatcher pump runs between these calls: verify native registration without processing live input.
                input.Enable(true);
                input.Enable(false);
                Check(keys.Count == 0 && hits == 0, "native background input registration and removal");
                int header = 8 + 2 * IntPtr.Size;
                byte[] data = new byte[header + 32];
                IntPtr ptr = Marshal.AllocHGlobal(data.Length);
                try
                {
                    Marshal.Copy(data, 0, ptr, data.Length);
                    Marshal.WriteInt32(ptr, 1);
                    Marshal.WriteInt16(ptr, header + 6, 83);
                    // Holding one physical key can deliver enough repeats to cross the speed threshold.
                    // Only the transition to down should reach the animation.
                    for (int repeat = 0; repeat < 15; repeat++)
                        input.DispatchPacket(ptr, (uint)data.Length);
                    Marshal.WriteInt16(ptr, header + 2, 1);
                    input.DispatchPacket(ptr, (uint)data.Length);
                    Marshal.WriteInt16(ptr, header + 2, 0);
                    input.DispatchPacket(ptr, (uint)data.Length);
                    Check(keys.SequenceEqual(new int[] { 83, 83 }), "physical press, repeat, release, repress");
                    Marshal.WriteInt32(ptr, 0);
                    Marshal.WriteInt16(ptr, header + 4, 4);
                    input.DispatchPacket(ptr, (uint)data.Length);
                    input.DispatchPacket(ptr, 3);
                    Check(hits == 1, "right mouse pulse and truncated packet");
                }
                finally { Marshal.FreeHGlobal(ptr); }
            }
        }

        private static void TestTypingRhythm()
        {
            var rhythm = new TypingRhythm();
            for (int i = 0; i < 9; i++) rhythm.Press(.4 * i);
            Check(!rhythm.Helping && rhythm.AgitatedSince < 0, "nine presses in four seconds are below 150 KPM");
            rhythm.Press(3.6);
            Check(rhythm.Helping && rhythm.AgitatedSince < 0, "ten presses activate the helping tail");

            for (int i = 10; i < 19; i++) rhythm.Press(.4 * i);
            Check(rhythm.Helping && rhythm.AgitatedSince < 0, "a brief fast streak does not trigger frantic swishing");
            rhythm.Press(7.61);
            Check(rhythm.AgitatedSince >= 0, "four more seconds of fast typing trigger the double slap");
            rhythm.Update(8.9);
            Check(!rhythm.Helping && rhythm.AgitatedSince < 0, "idle time resets both fast poses");

            for (int i = 0; i < 10; i++) rhythm.Press(20 + i * .1);
            rhythm.Press(80);
            Check(!rhythm.Helping && rhythm.AgitatedSince < 0, "first press after a delayed frame starts a fresh streak");

            for (int i = 1; i <= 9; i++) rhythm.Press(80 + i * .1);
            Check(rhythm.Helping, "fast typing can start again after a long break");
            rhythm.Reset();
            rhythm.Press(81);
            Check(!rhythm.Helping && rhythm.AgitatedSince < 0, "explicit reset discards previous typing activity");

            var slowing = new TypingRhythm();
            for (int i = 0; i < 10; i++) slowing.Press(.4 * i);
            slowing.Update(4.5);
            Check(slowing.Helping, "the tail keeps helping at eight recent presses");
            slowing.Press(4.6);
            slowing.Update(5.65);
            Check(!slowing.Helping, "slowing below eight presses ends tail assistance before idle");
        }

        private static byte[] Region(PetView view, Int32Rect rect)
        {
            var bitmap = Render(view);
            byte[] pixels = new byte[rect.Width * rect.Height * 4];
            bitmap.CopyPixels(rect, pixels, rect.Width * 4, 0);
            return pixels;
        }

        private static void TestTypingPoses()
        {
            double now = 0;
            Func<double> clock = delegate { return now; };
            var pet = new PetView(clock);
            var calm = new PetView(clock);

            // At the render scale of 512 x 460, this strip covers the left hind foot above the keyboard.
            var hindPaws = new Int32Rect(174, 331, 43, 29);
            pet.PressKey(32, "");
            calm.PressKey(76, "");
            now += .06;
            pet.Tick(.06); calm.Tick(.06);
            Check(!Region(pet, hindPaws).SequenceEqual(Region(calm, hindPaws)), "space and letter strikes have different lower-leg poses");

            // The right edge above the keyboard contains the tail but no eye, key label or moving forepaw.
            var tail = new Int32Rect(338, 236, 125, 123);
            pet.ClearInput();
            byte[] restingTail = Region(calm, tail);
            bool sawTailAssist = false;
            for (int i = 0; i < 14; i++)
            {
                now = 1 + .4 * i;
                pet.PressKey((i % 2 == 0) ? 65 : 76, "");
                now += .08;
                pet.Tick(.08);
                if (i >= 9 && !Region(pet, tail).SequenceEqual(restingTail)) sawTailAssist = true;
            }
            Check(sawTailAssist, "fast typing moves the tail even when floating labels are hidden");
            pet.ClearInput();
            Check(Region(pet, tail).SequenceEqual(restingTail), "clearing input immediately restores the resting tail");

            // Space keeps the typing work on the hind foot. Tail movement in this sequence
            // after several seconds must therefore come from the sustained-speed animation.
            byte[] firstAgitated = null;
            bool sawSwish = false;
            for (int frame = 0; frame < 240; frame++)
            {
                now = 10 + frame * .05;
                if (frame % 5 == 0) pet.PressKey(32, "");
                pet.Tick(.05);
                if (frame < 145 || frame % 3 != 0) continue;
                byte[] current = Region(pet, tail);
                if (firstAgitated == null) firstAgitated = current;
                else if (!current.SequenceEqual(firstAgitated)) { sawSwish = true; break; }
            }
            Check(sawSwish, "sustained fast typing animates the tail independently of each key strike");
        }

        private static void TestSettings(string temp)
        {
            string file = Path.Combine(temp, "pet.xml");
            var settings = new PetSettings();
            settings.Cat.Name = "阿墨";
            settings.Cat.LeftSock = true;
            settings.Cat.LeftCheek = true;
            settings.Cat.Pattern = "tuxedo";
            settings.Configured = true;
            settings.Save(file);
            settings.ShowKeys = false;
            settings.Save(file);
            var loaded = PetSettings.Load(file);
            Check(loaded.Configured && !loaded.ShowKeys && loaded.Cat.Name == "阿墨"
                && loaded.Cat.LeftCheek && loaded.Cat.LeftSock && !loaded.Cat.RightSock, "atomic settings round trip");
            var copy = loaded.Cat.Copy(); copy.Name = "copy";
            Check(loaded.Cat.Name != copy.Name, "editor copy");
            File.WriteAllText(file, "<broken");
            Check(!PetSettings.Load(file).Configured, "corrupt settings recovery");
            copy.Fur = "oops"; copy.Build = double.NaN; copy.Normalize();
            Check(copy.Fur == "#232127" && copy.Build == 1, "invalid appearance defaults");
        }

        private static void TestPhoto(string temp)
        {
            string file = Path.Combine(temp, "cat.png");
            byte[] pixels = new byte[32 * 32 * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 60; pixels[i + 1] = 104; pixels[i + 2] = 184; pixels[i + 3] = 255;
            }
            var image = BitmapSource.Create(32, 32, 96, 96, PixelFormats.Bgra32, null, pixels, 128);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using (var stream = File.Create(file)) encoder.Save(stream);
            var loaded = PhotoPalette.Read(file);
            Check(string.Equals(PhotoPalette.Sample(loaded, loaded.PixelWidth / 2, loaded.PixelHeight / 2), "#B8683C",
                StringComparison.OrdinalIgnoreCase), "local photo color sampling");
            var estimate = (Color)ColorConverter.ConvertFromString(PhotoPalette.EstimateFur(loaded));
            Check(estimate.R > estimate.G && estimate.G > estimate.B, "photo initializes a warm coat");
            using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }

        private static RenderTargetBitmap Render(PetView view)
        {
            var size = new Size(512, 460);
            view.Measure(size);
            view.Arrange(new Rect(new Point(), size)); view.UpdateLayout();
            var bitmap = new RenderTargetBitmap(512, 460, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(view);
            return bitmap;
        }

        private static byte[] Pixels(BitmapSource bitmap)
        {
            byte[] values = new byte[bitmap.PixelHeight * bitmap.PixelWidth * 4];
            bitmap.CopyPixels(values, bitmap.PixelWidth * 4, 0);
            return values;
        }

        private static PetView TestArt()
        {
            var pet = new PetView { Look = new CatLook() };
            var first = Pixels(Render(pet));
            int opaque = 0, head = 0;
            for (int i = 3; i < first.Length; i += 4) if (first[i] > 200) opaque++;
            Check(opaque > 8000 && opaque < 190000 && first[3] == 0, "transparent desktop sprite");
            for (int y = 5; y < 460; y += 10)
                for (int x = 5; x < 512; x += 10) if (pet.IsHead(new Point(x,y))) head++;
            Check(head > 5 && head < 1000 && !pet.IsHead(new Point(-20,-20)), "head hover region");
            pet.PressKey(65, "A"); pet.PressKey(83, "S"); pet.PressKey(68, "D");
            pet.SetPointer(-.65,.42,true,false); pet.Tick(.04);
            Check(!Pixels(Render(pet)).SequenceEqual(first), "mouse and typing pose changes");
            var watching = new PetView { Look = new CatLook() };
            var enjoying = new PetView { Look = new CatLook() };
            watching.SetPointer(0,0,false,false); enjoying.SetPointer(0,0,false,true);
            for (int i = 0; i < 22; i++) { watching.Tick(.03); enjoying.Tick(.03); }
            Check(!Pixels(Render(watching)).SequenceEqual(Pixels(Render(enjoying))), "head hover changes pose without input");
            pet.ClearInput();
            var tux = pet.Look.Copy();
            tux.RightSock = true; tux.RightCheek = true; tux.Pattern = "tuxedo";
            pet.Look = tux; pet.SetPointer(-.5,.2,false,false);
            for (int i = 0; i < 24; i++) pet.Tick(.03);
            pet.PressKey(83,"S"); pet.Tick(.075);
            return pet;
        }

        private static void TestEditorAndWindow()
        {
            var initial = new CatLook();
            var editor = new CatEditor(initial);
            editor.ShowActivated = false;
            editor.ShowInTaskbar = false;
            editor.WindowStartupLocation = WindowStartupLocation.Manual;
            editor.Left = -12000; editor.Top = -12000;
            editor.Width = editor.MinWidth;
            editor.Show(); editor.UpdateLayout();
            Check(HasVisiblePrivacyNotice(editor), "local input privacy notice is visible at the minimum editor width");
            editor.Close();
            Check(initial.Name == "小黑", "editor opens and cancellation preserves appearance");
            // The shell is explicitly paused, so this test never monitors live user input.
            var window = new PetWindow(new PetSettings { Paused = true, Configured = true });
            window.Opacity = 0;
            window.Show();
            window.Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate {}));
            Check(window.Topmost && window.AllowsTransparency && !window.ShowInTaskbar, "desktop window lifecycle");
            window.Close();
        }

        private static bool HasVisiblePrivacyNotice(DependencyObject element)
        {
            var text = element as System.Windows.Controls.TextBlock;
            if (text != null && text.IsVisible && text.ActualHeight > 0 && text.ActualWidth > 0
                && text.Text.Contains("不保存按键历史") && text.Text.Contains("不上传按键"))
                return true;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                if (HasVisiblePrivacyNotice(VisualTreeHelper.GetChild(element, i))) return true;
            return false;
        }
    }
}
