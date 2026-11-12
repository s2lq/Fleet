using System;
using System.Collections.Generic;
using System.Drawing;

namespace Fleet
{
    // where the windows go. The slots share their edges, so they cover the screen exactly and never overlap.
    public static class Layouts
    {
        const double Wanted = 1.6;      // the shape of window that looks best

        // n slots inside area. Rows get an even number of windows (the first rows one more when it does not divide),
        // and the number of rows is chosen so the windows come out as close to the wanted shape as possible.
        // fits is false when the slots would be smaller than min.
        public static List<Rectangle> Slots(int n, Rectangle area, Size min, out bool fits)
        {
            n = Math.Max(1, n);
            area.Width = Math.Max(1, area.Width); area.Height = Math.Max(1, area.Height);
            int bestRows = 1;
            double best = double.MaxValue;
            for (int r = 1; r <= n; r++)
            {
                int baseK = n / r, extra = n % r;
                double ch = area.Height / (double)r, score = 0, lack = 0;
                for (int j = 0; j < r; j++)
                {
                    int k = baseK + (j < extra ? 1 : 0);
                    double cw = area.Width / (double)k;
                    score += k * Math.Abs(Math.Log((cw / ch) / Wanted));
                    if (min.Width > 0) lack += k * Math.Max(0, min.Width - cw) / min.Width;
                    if (min.Height > 0) lack += k * Math.Max(0, min.Height - ch) / min.Height;
                }
                score = score / n + lack * 10 + (extra != 0 ? 0.04 : 0);
                if (score < best - 1e-9) { best = score; bestRows = r; }
            }
            return Build(n, area, bestRows, min, out fits);
        }

        static List<Rectangle> Build(int n, Rectangle area, int rows, Size min, out bool fits)
        {
            List<Rectangle> list = new List<Rectangle>();
            int baseK = n / rows, extra = n % rows;
            fits = true;
            for (int j = 0; j < rows; j++)
            {
                int k = baseK + (j < extra ? 1 : 0);
                int y0 = area.Y + (int)Math.Round(j * area.Height / (double)rows), y1 = area.Y + (int)Math.Round((j + 1) * area.Height / (double)rows);
                for (int i = 0; i < k; i++)
                {
                    int x0 = area.X + (int)Math.Round(i * area.Width / (double)k), x1 = area.X + (int)Math.Round((i + 1) * area.Width / (double)k);
                    Rectangle r = new Rectangle(x0, y0, x1 - x0, y1 - y0);
                    if (r.Width < min.Width || r.Height < min.Height) fits = false;
                    list.Add(r);
                }
            }
            return list;
        }

        public static List<Rectangle> Slots(int n, Rectangle area, Size min)
        {
            bool f;
            return Slots(n, area, min, out f);
        }

        // the most windows that fit on this area without any being smaller than min
        public static int MaxFit(Rectangle area, Size min)
        {
            int best = 0;
            for (int n = 1; n <= 80; n++)
            {
                bool f;
                Slots(n, area, min, out f);
                if (!f) break;
                best = n;
            }
            return best;
        }
    }
}
