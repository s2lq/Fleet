using System;
using System.Collections.Generic;
using System.Drawing;

namespace Fleet
{
    // where the windows go: a grid that fits the screen shape, as fractions of a screen (0 to 1)
    public static class Layouts
    {
        // columns and rows that fit n windows best on a screen of this shape (cols > 0 forces the columns)
        public static void BestGrid(int n, double screenAspect, int cols, out int c, out int r)
        {
            n = Math.Max(1, n);
            if (cols > 0) { c = Math.Min(cols, n); r = (int)Math.Ceiling(n / (double)c); return; }
            double best = double.MaxValue; c = 1; r = n;
            for (int cc = 1; cc <= n; cc++)
            {
                int rr = (int)Math.Ceiling(n / (double)cc);
                double cell = screenAspect * rr / cc;                  // width / height of one window
                double score = Math.Abs(Math.Log(cell / 1.6)) + (cc * rr - n) * 0.12;
                if (score < best) { best = score; c = cc; r = rr; }
            }
        }

        // the slots for n windows. The last row may be short: its windows get wider, so the screen stays full
        public static List<RectangleF> Smart(int n, double aspect)
        {
            List<RectangleF> list = new List<RectangleF>();
            int c, r;
            BestGrid(n, aspect, 0, out c, out r);
            for (int i = 0; i < n; i++)
            {
                int row = i / c, inRow = Math.Min(c, n - row * c), col = i % c;
                list.Add(new RectangleF(col / (float)inRow, row / (float)r, 1f / inRow, 1f / r));
            }
            return list;
        }
    }
}
