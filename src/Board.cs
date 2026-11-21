using System;
using System.Collections.Generic;
using System.Windows;

namespace PawsOnKeys
{
    internal static class Board
    {
        internal sealed class Cap
        {
            internal int Vk;
            internal double X, Y, W;
            internal Point Center
            {
                get
                {
                    return new Point(X + W / 2, Y + 3.4 + Slope(X + W / 2));
                }
            }
        }

        internal static readonly List<Cap> Keys = new List<Cap>();
        private static readonly Dictionary<int, Cap> byCode = new Dictionary<int, Cap>();
        internal static double Slope(double x)
        {
            return (x - 84) * .074;
        }

        static Board()
        {
            Row(0, new int[] { 27, 49, 50, 51, 52, 53, 54, 55, 56, 57, 48, 189, 187, 8, 45, 36 }, null);
            Row(1, new int[] { 9, 81, 87, 69, 82, 84, 89, 85, 73, 79, 80, 219, 221, 220, 46, 35 }, null);
            Row(2, new int[] { 20, 65, 83, 68, 70, 71, 72, 74, 75, 76, 186, 222, 13, 33 }, new double[] { 1.5, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 1 });
            Row(3, new int[] { 160, 90, 88, 67, 86, 66, 78, 77, 188, 190, 191, 161, 38, 34 }, new double[] { 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1.65, 1, 1 });
            Row(4, new int[] { 162, 91, 164, 32, 165, 163, 37, 40, 39 }, new double[] { 1.4, 1.2, 1.2, 5.8, 1.2, 1.4, 1, 1, 1 });
        }

        private static void Row(int row, int[] codes, double[] units)
        {
            double count = units == null ? codes.Length : 0;
            if (units != null)
                foreach (double u in units)
                    count += u;
            double step = (240 + row * 2.5) / count, x = 86 - row * 1.5;
            for (int i = 0; i < codes.Length; i++)
            {
                double w = step * (units == null ? 1 : units[i]);
                var cap = new Cap
                {
                    Vk = codes[i],
                    X = x,
                    Y = 275 + row * 7.8,
                    W = w - 1.4
                };
                Keys.Add(cap);
                byCode.Add(codes[i], cap);
                x += w;
            }
        }

        internal static Cap Find(int key)
        {
            if (key >= 96 && key <= 105)
                key = 48 + (key - 96);
            if (key == 16)
                key = 160;
            if (key == 17)
                key = 162;
            if (key == 18)
                key = 164;
            if (key == 92)
                key = 91;
            Cap cap;
            return byCode.TryGetValue(key, out cap) ? cap : null;
        }
    }
}
