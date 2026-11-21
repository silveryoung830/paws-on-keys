using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace PawsOnKeys
{
    internal static class TrayArtwork
    {
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);

        internal static Icon MakeIcon()
        {
            using (var bitmap = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bitmap))
            using (var fur = new SolidBrush(Color.FromArgb(44, 42, 48)))
            using (var eye = new SolidBrush(Color.FromArgb(222, 243, 107)))
            using (var edge = new Pen(Color.FromArgb(204, 202, 187), 1.4f))
            using (var shape = new GraphicsPath())
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                shape.AddPolygon(new[] { new Point(3,15), new Point(4,3), new Point(11,8), new Point(21,8), new Point(28,3), new Point(29,16), new Point(27,24), new Point(21,28), new Point(11,28), new Point(5,24) });
                g.FillPath(fur, shape); g.DrawPath(edge, shape);
                g.FillEllipse(eye,7,13,8,8); g.FillEllipse(eye,18,13,8,8);
                g.FillEllipse(fur,11,15,3,4); g.FillEllipse(fur,22,15,3,4);
                IntPtr handle = bitmap.GetHicon();
                try { using (Icon icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
    }
}