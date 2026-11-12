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

        Rectangle area = new Rectangle(0, 0, 1920, 1040);
        bool ok = true; string why = "";
        for (int n = 1; n <= 40; n++)
        {
            List<Rectangle> s = Layouts.Slots(n, area, Size.Empty);
            long total = s.Sum(r => (long)r.Width * r.Height);
            if (s.Count != n) { ok = false; why = "count for " + n; }
            else if (total != 1920L * 1040) { ok = false; why = "coverage " + total + " for " + n; }
            else if (s.Any(r => r.X < 0 || r.Y < 0 || r.Right > 1920 || r.Bottom > 1040 || r.Width < 1 || r.Height < 1)) { ok = false; why = "bounds for " + n; }
            else
                for (int i = 0; i < s.Count && ok; i++)
                    for (int j = i + 1; j < s.Count; j++)
                        if (Rectangle.Intersect(s[i], s[j]).Width > 0 && Rectangle.Intersect(s[i], s[j]).Height > 0) { ok = false; why = "overlap for " + n; break; }
        }
        Chk(ok, "n windows fill the screen exactly and never overlap for n = 1 to 40 " + why);

        Chk(Layouts.Slots(6, area, Size.Empty).Select(r => r.Y).Distinct().Count() == 2, "6 windows -> 2 rows");
        Chk(Layouts.Slots(4, area, Size.Empty).Select(r => r.Y).Distinct().Count() == 2, "4 windows -> 2 x 2");
        Chk(Layouts.Slots(9, area, Size.Empty).Select(r => r.Y).Distinct().Count() == 3, "9 windows -> 3 rows");
        Chk(Layouts.Slots(2, area, Size.Empty).Select(r => r.Y).Distinct().Count() == 1, "2 windows side by side");
        Chk(Layouts.Slots(3, new Rectangle(0, 0, 1000, 1800), Size.Empty).Select(r => r.Y).Distinct().Count() == 3, "3 windows on a portrait screen stack");
        List<Rectangle> seven = Layouts.Slots(7, area, Size.Empty);
        Chk(seven.Max(r => r.Width) - seven.Min(r => r.Width) < 1920 / 2 - 1920 / 3 + 3, "7 windows: rows are balanced, no single wide leftover");

        bool fits;
        Layouts.Slots(50, area, new Size(400, 300), out fits);
        Chk(!fits, "50 windows do not fit at 400 x 300");
        Layouts.Slots(6, area, new Size(400, 300), out fits);
        Chk(fits, "6 windows fit at 400 x 300");
        int cap = Layouts.MaxFit(area, new Size(480, 320));
        Chk(cap >= 12 && cap <= 15, "a 1080p screen holds 12 to 15 windows at 480 x 320 (" + cap + ")");
        int[] fit = Engine.Fit(new int[] { 10, 2 }, new int[] { 6, 8 });
        Chk(fit[0] == 6 && fit[1] == 6 && fit.Sum() == 12, "windows that do not fit move to the other screen");
        return fails;
    }
}
