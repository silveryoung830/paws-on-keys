using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly TypingRhythm rhythm = new TypingRhythm();
        private readonly Func<double> readTime;
        private CatLook look;
        private Brush fur, mark, eye;
        public PetView() : this(delegate { return (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency; }) { }

        internal PetView(Func<double> readTime)
        {
            this.readTime = readTime;
            Look = new CatLook();
        }

        private double time, aimX, aimY, x, y, affection, strike, mousePress;
        private bool pressing;
        private Vector gazeTarget, gaze;
        private double pettingRemaining;
        private static readonly Point EyeMidpoint = new Point(160, 104.5);
        private Board.Cap key;
        private static readonly Point RestingForepaw = new Point(220, 269);
        private static readonly Point RestingFoot = new Point(149, 265);
        private Point forepawTarget = RestingForepaw, tailTarget;
        private double forepawStrike, footAge = 1, tailAge = 1, slapPhase = -1;
        private bool nextIsTail = true;
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

        public void SetPointer(double dx, double dy, bool down, Point local)
        {
            aimX = Math.Max(-1, Math.Min(1, dx));
            aimY = Math.Max(-1, Math.Min(1, dy));
            pressing = down;
            gazeTarget = new Vector();
            Point facePoint;
            if (!TryFacePoint(local, out facePoint)) return;
            Vector direction = facePoint - EyeMidpoint;
            double distance = direction.Length;
            // Gradually lose interest as the pointer leaves the area around the cat.
            double interest = Math.Max(0, Math.Min(1, (305 - distance) / 120));
            gazeTarget = direction * (interest / Math.Max(30, distance));
        }

        public void PetHead()
        {
            pettingRemaining = .65;
        }

        public void PressKey(int vk, string label)
        {
            double now = readTime();
            rhythm.Press(now);
            UpdateTailState(now);
            key = Board.Find(vk);
            strike = 1;
            if (vk == 32)
            {
                footAge = 0;
            }
            else if (rhythm.Helping && key != null && slapPhase < 0 && nextIsTail)
            {
                tailTarget = key.Center;
                tailAge = 0;
                nextIsTail = false;
            }
            else
            {
                forepawTarget = Target();
                forepawStrike = 1;
                nextIsTail = true;
            }
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
            double now = readTime();
            rhythm.Update(now);
            UpdateTailState(now);
            footAge = Math.Min(1, footAge + d);
            tailAge = Math.Min(1, tailAge + d);
            forepawStrike = Math.Max(0, forepawStrike - d * 3.6);
            time += d;
            x += (aimX - x) * k;
            y += (aimY - y) * k;
            gaze += (gazeTarget - gaze) * k;
            affection += ((pettingRemaining > 0 ? 1 : 0) - affection) * (1 - Math.Exp(-9 * d));
            pettingRemaining = Math.Max(0, pettingRemaining - d);
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
            rhythm.Reset();
            nextIsTail = true;
            footAge = tailAge = 1;
            forepawStrike = 0;
            forepawTarget = RestingForepaw;
            slapPhase = -1;
            key = null;
            echoes.Clear();
            strike = 0;
            pressing = false;
            aimX = aimY = x = y = 0;
            gazeTarget = gaze = new Vector();
            mousePress = affection = pettingRemaining = 0;
            InvalidateVisual();
        }

        private void UpdateTailState(double now)
        {
            if (!rhythm.Helping) nextIsTail = true;
            slapPhase = -1;
            if (rhythm.AgitatedSince < 0) return;
            // Two quiet impacts in a short burst. The tail can type again between bursts.
            double phase = (now - rhythm.AgitatedSince) % 2.6;
            if (phase < .28) slapPhase = phase;
            else if (phase >= .36 && phase < .64) slapPhase = phase - .36;
        }

        private static double Reach(double age)
        {
            if (age < .06) return Math.Sin(Math.PI * .5 * age / .06);
            if (age <= .11) return 1;
            if (age >= .28) return 0;
            return .5 + .5 * Math.Cos(Math.PI * (age - .11) / .17);
        }

        private static Point Blend(Point start, Point end, double reach)
        {
            return start + (end - start) * reach;
        }

        private Matrix ViewTransform
        {
            get
            {
                double scale = Math.Min(ActualWidth / 390.0, ActualHeight / 355.0);
                return new Matrix(scale, 0, 0, scale, (ActualWidth - 390 * scale) / 2, (ActualHeight - 355 * scale) / 2);
            }
        }

        private Matrix HeadTransform
        {
            get
            {
                Matrix matrix = Matrix.Identity;
                matrix.Translate(0, -3 * affection + Math.Sin(time * 1.7) * .6);
                matrix.RotateAt(-6 * affection, 162, 147);
                return matrix;
            }
        }

        private Geometry HeadShape { get { return slapPhase >= 0 && affection < .4 ? CrossFace : Face; } }

        private Matrix FaceToView { get { return HeadTransform * ViewTransform; } }

        internal Geometry HeadHitGeometry
        {
            get
            {
                Geometry hit = HeadShape.Clone();
                hit.Transform = new MatrixTransform(FaceToView);
                hit.Freeze();
                return hit;
            }
        }

        private bool TryFacePoint(Point local, out Point point)
        {
            point = local;
            Matrix faceToView = FaceToView;
            if (!faceToView.HasInverse) return false;
            faceToView.Invert();
            point = faceToView.Transform(local);
            return true;
        }

        public bool IsHead(Point local)
        {
            Point facePoint;
            return TryFacePoint(local, out facePoint) && HeadShape.FillContains(facePoint);
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
        private static readonly Geometry CrossFace = MakeGeometry("M110,100 Q111,84 121,77 L116,57 Q115,50 122,53 L151,63 Q165,58 180,64 L206,56 Q214,53 211,62 L206,85 Q222,111 202,136 Q183,153 153,147 Q121,145 112,124 Q108,114 110,100 Z");
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

        private static void Curve(DrawingContext d, Pen pen, Point start, Point first, Point second, Point end)
        {
            var path = new StreamGeometry();
            using (var trace = path.Open())
            {
                trace.BeginFigure(start, false, false);
                trace.BezierTo(first, second, end, true, false);
            }
            path.Freeze();
            d.DrawGeometry(null, pen, path);
        }

        private static void DrawKickingFoot(DrawingContext d, Brush coat, double reach)
        {
            Point end = Blend(RestingFoot, Board.Find(32).Center, reach);
            var leg = new Pen(coat, 12) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            Curve(d, leg, new Point(152, 245), new Point(147 - 14 * reach, 251),
                new Point(end.X - 18, end.Y - 7), end);
            Oval(d, coat, end.X, end.Y, 15, 8);
            if (reach > .6)
            {
                d.PushOpacity(.38 * reach);
                for (int i = 0; i < 2; i++)
                {
                    double toe = end.X + 4 + i * 4;
                    Stroke(d, Whisker, 1, new Point(toe, end.Y + 5), new Point(toe + 1, end.Y + 2));
                }
                d.Pop();
            }
        }

        protected override void OnRender(DrawingContext d)
        {
            base.OnRender(d);
            Matrix view = ViewTransform;
            if (!view.HasInverse)
                return;
            d.PushTransform(new MatrixTransform(view));
            var tailPen = new Pen(fur, 11)
            {
                EndLineCap = PenLineCap.Round,
                StartLineCap = PenLineCap.Round
            };
            bool slapping = slapPhase >= 0;
            double tailReach = Reach(slapping ? slapPhase : tailAge);
            Point tipTarget = slapping ? new Point(349, 292) : tailTarget;
            Point bend = Blend(new Point(279, 224), new Point(292, 263), tailReach);
            Point tip = Blend(new Point(309, 205), tipTarget, tailReach);
            if (tailReach > 0)
            {
                Curve(d, tailPen, new Point(226, 244), new Point(252, 258),
                    Blend(new Point(265, 248), new Point(276, 252), tailReach), bend);
            }
            else d.DrawGeometry(null, tailPen, Tail);
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
            double footReach = Reach(footAge);
            if (footReach == 0) Oval(d, fur, RestingFoot.X, RestingFoot.Y, 16, 9);
            Oval(d, fur, 226, 267, 17, 9);
            d.PushTransform(new MatrixTransform(HeadTransform));
            Geometry face = HeadShape;
            d.DrawGeometry(fur, null, face);
            d.PushClip(face);
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
                    Oval(d, Ink, cx + gaze.X * 7, cy + gaze.Y * 7 * open, 5.6, 8 * open);
                    d.Pop();
                }

                double edge = i == 0 ? 117 : 205, dir = i == 0 ? -1 : 1;
                for (int j = 0; j < 2; j++)
                    Stroke(d, Whisker, .9, new Point(edge, 122 + j * 4), new Point(edge + dir * (9 + j * 1.5), 122 + j * 5));
            }

            d.Pop();
            d.DrawDrawing(Keyboard);
            if (key != null && strike > .36)
                DrawCap(d, key, true);
            if (footReach > 0)
                DrawKickingFoot(d, fur, footReach);
            if (tailReach > 0)
            {
                // Draw the returning end over the keys, with the root behind the cat.
                Curve(d, tailPen, bend, Blend(new Point(288, 209), new Point(310, 279), tailReach),
                    Blend(new Point(298, 204), new Point(tipTarget.X + 13, tipTarget.Y - 9), tailReach), tip);
                if (slapping && tailReach > .88)
                {
                    Stroke(d, Gold, 1.7, new Point(352, 285), new Point(355, 280));
                    Stroke(d, Gold, 1.7, new Point(356, 290), new Point(362, 288));
                }
            }
            double mx = 55 + x * 13, my = 285 + y * 9;
            Oval(d, Rim, mx, my + 1.7, 13, 15);
            Oval(d, Tile, mx, my, 12.3, 14);
            Stroke(d, mousePress > .25 ? Gold : Front, 1.8, new Point(mx + 1, my - 5), new Point(mx, my));
            var mousePaw = new Point(mx, my - 7 + mousePress * 2);
            DrawArm(d, fur, Tile, look.LeftSock, new Point(144, 161), mousePaw);
            var typing = Blend(RestingForepaw, forepawTarget, Math.Min(1, forepawStrike * 2));
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
