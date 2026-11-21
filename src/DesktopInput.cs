using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PawsOnKeys
{
    // Only physical key transitions are used. No character translation or input history.
    internal sealed class DesktopInput : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Device { public ushort Page, Usage; public uint Flags; public IntPtr Target; }
        [StructLayout(LayoutKind.Sequential)]
        private struct Header { public uint Type, Size; public IntPtr Device, WParam; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct ScreenPoint { public int X, Y; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(Device[] devices, uint count, uint size);
        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
        [DllImport("user32.dll")]
        internal static extern bool GetCursorPos(out ScreenPoint point);
        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int key);

        private readonly HwndSource source;
        private readonly Action<int> keyDown;
        private readonly Action mouseDown;
        private readonly HashSet<int> held = new HashSet<int>();
        private readonly uint headerSize = (uint)Marshal.SizeOf(typeof(Header));
        private IntPtr buffer = Marshal.AllocHGlobal(128);
        private bool active, registered;

        internal DesktopInput(HwndSource window, Action<int> onKey, Action onMouse)
        {
            source = window;
            keyDown = onKey;
            mouseDown = onMouse;
            source.AddHook(HandleMessage);
        }

        internal void Enable(bool value)
        {
            // Stop dispatching immediately, even if Windows cannot unregister the devices.
            if (!value) { active = false; held.Clear(); }
            if (registered == value) { active = value; return; }
            var devices = new Device[2];
            for (int i = 0; i < devices.Length; i++)
                devices[i] = new Device { Page = 1, Usage = (ushort)(i == 0 ? 6 : 2), Flags = value ? 0x100u : 1u, Target = value ? source.Handle : IntPtr.Zero };
            if (!RegisterRawInputDevices(devices, 2, (uint)Marshal.SizeOf(typeof(Device))))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            held.Clear();
            registered = active = value;
        }

        private IntPtr HandleMessage(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled)
        {
            if (msg != 0xff || !active) return IntPtr.Zero;
            uint size = 128;
            uint actual = GetRawInputData(lp, 0x10000003, buffer, ref size, headerSize);
            if (actual != uint.MaxValue) DispatchPacket(buffer, actual);
            return IntPtr.Zero;
        }

        // Separating the packet decoder permits synthetic regression checks without injecting system input.
        internal void DispatchPacket(IntPtr data, uint actual)
        {
            if (actual < headerSize + 8) return;
            int offset = (int)headerSize;
            int type = Marshal.ReadInt32(data);
            if (type == 0)
            {
                int flags = (ushort)Marshal.ReadInt16(data, offset + 4);
                if ((flags & 0x15) != 0) mouseDown();
            }
            else if (type == 1)
            {
                int vk = (ushort)Marshal.ReadInt16(data, offset + 6);
                int flags = (ushort)Marshal.ReadInt16(data, offset + 2);
                int scan = (ushort)Marshal.ReadInt16(data, offset);
                if (vk == 0 || vk == 255) return;
                if (vk == 0x10) vk = scan == 0x36 ? 0xA1 : 0xA0;
                if (vk == 0x11) vk = (flags & 2) != 0 ? 0xA3 : 0xA2;
                if (vk == 0x12) vk = (flags & 2) != 0 ? 0xA5 : 0xA4;
                if ((flags & 1) != 0) held.Remove(vk);
                else if (held.Add(vk)) keyDown(vk);
            }
        }

        internal static string KeyLabel(int key)
        {
            if ((key >= 0x41 && key <= 0x5a) || (key >= 0x30 && key <= 0x39)) return ((char)key).ToString();
            if (key >= 0x70 && key <= 0x87) return "F" + (key - 0x6f);
            if (key >= 0x60 && key <= 0x69) return "Num " + (key - 0x60);
            switch (key)
            {
                case 8: return "⌫"; case 9: return "Tab"; case 13: return "Enter";
                case 16: case 160: case 161: return "Shift";
                case 17: case 162: case 163: return "Ctrl";
                case 18: case 164: case 165: return "Alt";
                case 19: return "Pause"; case 20: return "Caps"; case 27: return "Esc"; case 32: return "Space";
                case 33: return "PgUp"; case 34: return "PgDn"; case 35: return "End"; case 36: return "Home";
                case 37: return "←"; case 38: return "↑"; case 39: return "→"; case 40: return "↓";
                case 44: return "PrtSc"; case 45: return "Ins"; case 46: return "Del";
                case 91: case 92: return "Win"; case 93: return "Menu";
                case 106: return "Num *"; case 107: return "Num +"; case 109: return "Num −"; case 110: return "Num ."; case 111: return "Num /";
                case 144: return "Num"; case 145: return "ScrLk";
                case 186: return ";"; case 187: return "="; case 188: return ",";
                case 189: return "-"; case 190: return "."; case 191: return "/";
                case 192: return "`"; case 219: return "["; case 220: case 226: return "\\"; case 221: return "]";
                case 222: return "'"; default: return System.Windows.Input.KeyInterop.KeyFromVirtualKey(key).ToString();
            }
        }

        public void Dispose()
        {
            try { if (registered) Enable(false); }
            catch (Win32Exception) { /* the HWND is being destroyed */ }
            finally
            {
                source.RemoveHook(HandleMessage);
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero;
            }
        }
    }
}