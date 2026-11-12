using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Fleet;

class LayoutTests
{
    static int fails;
    static void Chk(bool c, string n) { Console.WriteLine((c ? "PASS " : "FAIL ") + n); if (!c) fails++; }

    public static int Run()
    {
        int[] sp = Engine.Spread(7, new double[] { 2, 1 });
        Chk(sp.Sum() == 7 && sp[0] == 5 && sp[1] == 2, "7 windows over screens of size 2:1 -> 5 and 2");
        int[] sp3 = Engine.Spread(10, new double[] { 1, 1, 1 });
        Chk(sp3.Sum() == 10 && sp3.Max() - sp3.Min() <= 1, "10 windows over 3 equal screens differ by at most 1");
        Chk(Engine.Spread(1, new double[] { 1, 5 }).Sum() == 1, "1 window over 2 screens");

        bool ok = true; string why = "";
        for (int n = 1; n <= 30; n++)
        {
            List<RectangleF> s = Layouts.Smart(n, 16 / 9.0);
            float area = s.Sum(r => r.Width * r.Height);
            if (s.Count != n) { ok = false; why = "count for " + n; }
            else if (Math.Abs(area - 1f) > 0.02f) { ok = false; why = "coverage " + area + " for " + n; }
            else if (s.Any(r => r.X < -0.001f || r.Y < -0.001f || r.Right > 1.001f || r.Bottom > 1.001f)) { ok = false; why = "bounds for " + n; }
        }
        Chk(ok, "the grid gives n windows that fill the screen edge to edge for n = 1 to 30 " + why);

        int c, r2;
        Layouts.BestGrid(6, 16 / 9.0, 0, out c, out r2); Chk(c == 3 && r2 == 2, "6 windows on 16:9 -> 3 x 2 (" + c + "x" + r2 + ")");
        Layouts.BestGrid(4, 16 / 9.0, 0, out c, out r2); Chk(c == 2 && r2 == 2, "4 windows -> 2 x 2");
        Layouts.BestGrid(9, 16 / 9.0, 0, out c, out r2); Chk(c == 3 && r2 == 3, "9 windows -> 3 x 3");
        Layouts.BestGrid(2, 16 / 9.0, 0, out c, out r2); Chk(c == 2 && r2 == 1, "2 windows side by side on 16:9");
        Layouts.BestGrid(3, 9 / 16.0, 0, out c, out r2); Chk(c == 1 && r2 == 3, "3 windows on a portrait screen stack");
        return fails;
    }
}
