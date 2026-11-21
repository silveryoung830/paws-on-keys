using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace PawsOnKeys
{
    public sealed class PetView : FrameworkElement
    {
        private sealed class Echo
        {
            public Point Start;
            public string Label;
            public double Age;
            public int Lane;
        }

        private readonly List<Echo> echoes = new List<Echo>();
        private CatLook look;
        private Brush fur, mark, eye;
        public PetView()
        {
            Look = new CatLook();
        }

        private double time, aimX, aimY, x, y, affection, strike, mousePress;
        private bool touching, pressing;
        private Board.Cap key;
        private int sequence;
        public CatLook Look
        {
            get
            {
                return look;
            }

            set
            {
                look = value ?? new CatLook();
                fur = MakeBrush(look.Fur);
                mark = MakeBrush(look.Marking);
                eye = MakeBrush(look.Eyes);
                InvalidateVisual();
            }
        }

        public void SetPointer(double dx, double dy, bool down, bool petting)
        {
            aimX = Math.Max(-1, Math.Min(1, dx));
            aimY = Math.Max(-1, Math.Min(1, dy));
            pressing = down;
            touching = petting;
        }

        public void PressKey(int vk, string label)
        {
            key = Board.Find(vk);
            strike = 1;
            if (!String.IsNullOrEmpty(label))
            {
                if (echoes.Count >= 14)
                    echoes.RemoveAt(0);
                echoes.Add(new Echo { Start = Target(), Label = label, Lane = sequence++ % 3 - 1 });
            }

            InvalidateVisual();
        }

        private Point Target()
        {
            return key == null ? new Point(300, 288) : key.Center;
        }

        public void Tick(double seconds)
        {
            double d = Math.Max(0, Math.Min(.15, seconds)), k = 1 - Math.Exp(-15 * d);
            time += d;
            x += (aimX - x) * k;
            y += (aimY - y) * k;
            affection += ((touching ? 1 : 0) - affection) * (1 - Math.Exp(-9 * d));
            mousePress += ((pressing ? 1 : 0) - mousePress) * k;
            strike = Math.Max(0, strike - d * 3.6);
            for (int i = echoes.Count - 1; i >= 0; i--)
            {
                echoes[i].Age += d;
                if (echoes[i].Age > .86)
                    echoes.RemoveAt(i);
            }

            InvalidateVisual();
        }

        public void ClearInput()
        {
            echoes.Clear();
            strike = 0;
            touching = pressing = false;
            mousePress = affection = 0;
            InvalidateVisual();
        }

        private double Scale
        {
            get
            {
                return Math.Min(ActualWidth / 390.0, ActualHeight / 355.0);
            }
        }

        public bool IsHead(Point p)
        {
            double s = Scale;
            if (s <= 0)
                return false;
            double u = (p.X - (ActualWidth - 390 * s) / 2) / s, v = (p.Y - (ActualHeight - 355 * s) / 2) / s;
            return u >= 109 && u <= 212 && v >= 49 && v <= 144 && ((u - 159) * (u - 159) / 3000.0 + (v - 103) * (v - 103) / 3400.0 <= 1.15);
        }

        private static Brush MakeBrush(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        private static Geometry MakeGeometry(string path)
        {
            var g = Geometry.Parse(path);
            g.Freeze();
            return g;
        }

        private static void Oval(DrawingContext d, Brush b, double px, double py, double rx, double ry)
        {
            d.DrawEllipse(b, null, new Point(px, py), rx, ry);
        }

        private static void Stroke(DrawingContext d, Brush b, double w, Point a, Point z)
        {
            var pen = new Pen(b, w)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            d.DrawLine(pen, a, z);
        }

        private static readonly Brush Rim = MakeBrush("#28262D"), Tile = MakeBrush("#F9F6EB"), Front = MakeBrush("#BCB7A9"), Gold = MakeBrush("#E5A938"), Ink = MakeBrush("#211F24"), Whisker = MakeBrush("#B9B3A6"), Calico = MakeBrush("#BA7849");
        private static readonly Geometry Body = MakeGeometry("M139,138 C119,164 124,216 147,250 C161,271 202,282 235,270 C264,260 258,234 249,207 C234,165 208,145 188,133 Z");
        private static readonly Geometry Face = MakeGeometry("M110,100 Q111,84 122,73 L128,43 Q130,35 136,41 L152,59 Q166,55 180,60 L196,43 Q203,36 205,48 L208,82 Q222,111 202,136 Q183,153 153,147 Q121,145 112,124 Q108,114 110,100 Z");
        private static readonly Geometry Tail = MakeGeometry("M226,244 C252,258 265,248 279,224 C288,209 298,204 309,205");
        private static readonly Geometry Bib = MakeGeometry("M138,145 Q164,155 187,143 Q198,166 180,185 L171,177 166,193 158,181 153,187 Q141,171 138,145 Z");
        private static readonly DrawingGroup Keyboard = MakeKeyboard();
        private static DrawingGroup MakeKeyboard()
        {
            var drawing = new DrawingGroup();
            using (var d = drawing.Open())
            {
                d.DrawGeometry(Rim, null, MakeGeometry("M84,271 L331,289 L345,337 L72,317 Z"));
                foreach (var cap in Board.Keys)
                    DrawCap(d, cap, false);
            }

            drawing.Freeze();
            return drawing;
        }

        private static void DrawCap(DrawingContext d, Board.Cap cap, bool down)
        {
            double px = cap.X, py = cap.Y, w = cap.W, rise = Board.Slope(px);
            var rect = new Rect(px, py + rise + (down ? 1.2 : 0), w, 6.8);
            d.PushTransform(new RotateTransform(4.2, rect.X, rect.Y));
            d.DrawRoundedRectangle(Front, null, new Rect(rect.X, rect.Y + 1.4, w, 6.8), 1.1, 1.1);
            d.DrawRoundedRectangle(down || cap.Vk == 27 || cap.Vk == 39 ? Gold : Tile, null, rect, 1.1, 1.1);
            if (cap.Vk >= 65 && cap.Vk <= 90)
                DrawLabel(d, ((char)cap.Vk).ToString(), 3.5, Front, new Point(rect.X + w / 2, rect.Y + 3));
            d.Pop();
        }

        private static void DrawLabel(DrawingContext dc, string text, double size, Brush b, Point at)
        {
            var lab = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, b, 1);
            dc.DrawText(lab, new Point(at.X - lab.Width / 2, at.Y - lab.Height / 2));
        }

        protected override void OnRender(DrawingContext d)
        {
            base.OnRender(d);
            double s = Scale;
            if (s <= 0)
                return;
            d.PushTransform(new MatrixTransform(s, 0, 0, s, (ActualWidth - 390 * s) / 2, (ActualHeight - 355 * s) / 2));
            var tailPen = new Pen(fur, 11)
            {
                EndLineCap = PenLineCap.Round,
                StartLineCap = PenLineCap.Round
            };
            d.DrawGeometry(null, tailPen, Tail);
            d.PushTransform(new ScaleTransform(look.Build, 1, 178, 230));
            d.DrawGeometry(fur, null, Body);
            if (look.Pattern == "tuxedo")
                d.DrawGeometry(mark, null, Bib);
            if (look.Pattern == "calico")
            {
                d.PushClip(Body);
                Oval(d, mark, 224, 185, 23, 36);
                Oval(d, Calico, 187, 246, 20, 28);
                d.Pop();
            }

            if (look.Pattern == "tabby")
            {
                d.PushClip(Body);
                for (int i = 0; i < 3; i++)
                    Stroke(d, mark, 5, new Point(221 + i * 5, 181 + i * 18), new Point(241 + i * 4, 170 + i * 18));
                d.Pop();
            }

            d.Pop();
            Oval(d, fur, 149, 265, 16, 9);
            Oval(d, fur, 226, 267, 17, 9);
            d.PushTransform(new RotateTransform(-6 * affection, 162, 147));
            d.PushTransform(new TranslateTransform(0, -3 * affection + Math.Sin(time * 1.7) * .6));
            d.DrawGeometry(fur, null, Face);
            d.PushClip(Face);
            if (look.LeftCheek)
                Oval(d, mark, 117, 127, 18, 11);
            if (look.RightCheek)
                Oval(d, mark, 206, 123, 19, 11);
            if (look.Pattern == "tabby")
            {
                for (int i = 0; i < 3; i++)
                    Stroke(d, mark, 3, new Point(152 + i * 8, 58), new Point(156 + i * 7, 76 + i % 2 * 4));
            }

            if (look.Pattern == "calico")
                Oval(d, mark, 123, 71, 24, 29);
            d.Pop();
            double blink = (time % 5.9) > 5.74 ? Math.Sin((time % 5.9 - 5.74) / .16 * Math.PI) : 0;
            double open = Math.Max(.1, 1 - Math.Max(blink, affection));
            for (int i = 0; i < 2; i++)
            {
                double cx = 139 + i * 42, cy = 106 - i * 3;
                Oval(d, eye, cx, cy, 13, 15 * open);
                if (open > .23)
                {
                    d.PushClip(new EllipseGeometry(new Point(cx, cy), 13, 15 * open));
                    Oval(d, Ink, cx + x * 7 + 2.5, cy + y * 8 + 2, 5.6, 8 * open);
                    d.Pop();
                }

                double edge = i == 0 ? 117 : 205, dir = i == 0 ? -1 : 1;
                for (int j = 0; j < 2; j++)
                    Stroke(d, Whisker, .9, new Point(edge, 122 + j * 4), new Point(edge + dir * (9 + j * 1.5), 122 + j * 5));
            }

            d.Pop();
            d.Pop();
            d.DrawDrawing(Keyboard);
            if (key != null && strike > .36)
                DrawCap(d, key, true);
            double mx = 55 + x * 13, my = 285 + y * 9;
            Oval(d, Rim, mx, my + 1.7, 13, 15);
            Oval(d, Tile, mx, my, 12.3, 14);
            Stroke(d, mousePress > .25 ? Gold : Front, 1.8, new Point(mx + 1, my - 5), new Point(mx, my));
            var mousePaw = new Point(mx, my - 7 + mousePress * 2);
            DrawArm(d, fur, Tile, look.LeftSock, new Point(144, 161), mousePaw);
            Point at = Target();
            var typing = new Point(220 + (at.X - 220) * Math.Min(1, strike * 2), 269 + (at.Y - 269) * Math.Min(1, strike * 2));
            DrawArm(d, fur, Tile, look.RightSock, new Point(204, 160), typing);
            foreach (Echo e in echoes)
            {
                double t = e.Age / .86, ex = e.Start.X + e.Lane * 13 + t * 11, ey = e.Start.Y - 16 - 55 * t;
                double width = Math.Max(15, e.Label.Length * 5.5 + 6);
                d.PushOpacity(Math.Min(1, (1 - t) * 2.1));
                d.DrawRoundedRectangle(Tile, new Pen(Front, .65), new Rect(ex - width / 2, ey - 6.5, width, 13), 3, 3);
                DrawLabel(d, e.Label, 9, Ink, new Point(ex, ey));
                d.Pop();
            }

            d.Pop();
        }

        private static void DrawArm(DrawingContext d, Brush fur, Brush mark, bool sock, Point from, Point end)
        {
            Stroke(d, fur, 12, from, end);
            Oval(d, sock ? mark : fur, end.X, end.Y, 7.9, 6.9);
        }
    }
}
