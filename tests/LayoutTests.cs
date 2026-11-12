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
        Store.Cfg = new Settings();
        Chk(Layouts.Builtin.Count >= 35, "at least 35 built-in layouts (" + Layouts.Builtin.Count + ")");
        bool inside = Layouts.Builtin.All(d => d.Slots.Count > 0 && d.Slots.All(s => s.W > 0.05f && s.H > 0.05f && s.X >= -0.001f && s.Y >= -0.001f && s.X + s.W <= 1.001f && s.Y + s.H <= 1.001f));
        Chk(inside, "every built-in slot is on the screen and not tiny");
        Chk(Layouts.Builtin.Select(d => d.Name).Distinct().Count() == Layouts.Builtin.Count, "built-in names are unique");

        int[] sp = Engine.Spread(7, new double[] { 2, 1 });
        Chk(sp.Sum() == 7 && sp[0] == 5 && sp[1] == 2, "7 windows over screens of size 2:1 -> 5 and 2");
        int[] sp3 = Engine.Spread(10, new double[] { 1, 1, 1 });
        Chk(sp3.Sum() == 10 && sp3.Max() - sp3.Min() <= 1, "10 windows over 3 equal screens differ by at most 1");
        Chk(Engine.Spread(1, new double[] { 1, 5 }).Sum() == 1, "1 window over 2 screens");

        bool ok = true; string why = "";
        for (int n = 1; n <= 30; n++)
        {
            List<RectangleF> s = Layouts.SlotsFor(Layouts.Auto, n, 16 / 9.0, 0);
            float area = s.Sum(r => r.Width * r.Height);
            if (s.Count != n) { ok = false; why = "count for " + n; }
            else if (Math.Abs(area - 1f) > 0.02f && n > 1) { ok = false; why = "coverage " + area + " for " + n; }
            else if (s.Any(r => r.X < -0.001f || r.Y < -0.001f || r.Right > 1.001f || r.Bottom > 1.001f)) { ok = false; why = "bounds for " + n; }
        }
        Chk(ok, "smart grid gives n slots that fill the screen for n = 1 to 30 " + why);

        int c, r2;
        Layouts.BestGrid(6, 16 / 9.0, 0, out c, out r2); Chk(c == 3 && r2 == 2, "6 windows on 16:9 -> 3 x 2 (" + c + "x" + r2 + ")");
        Layouts.BestGrid(4, 16 / 9.0, 0, out c, out r2); Chk(c == 2 && r2 == 2, "4 windows -> 2 x 2");
        Layouts.BestGrid(9, 16 / 9.0, 0, out c, out r2); Chk(c == 3 && r2 == 3, "9 windows -> 3 x 3");
        Layouts.BestGrid(2, 16 / 9.0, 0, out c, out r2); Chk(c == 2 && r2 == 1, "2 windows side by side on 16:9");
        Layouts.BestGrid(3, 9 / 16.0, 0, out c, out r2); Chk(c == 1 && r2 == 3, "3 windows on a portrait screen stack");
        Layouts.BestGrid(5, 16 / 9.0, 2, out c, out r2); Chk(c == 2 && r2 == 3, "forced 2 columns for 5 windows");

        List<RectangleF> fixedSlots = Layouts.SlotsFor("Main left + 3 side", 4, 16 / 9.0, 0);
        Chk(fixedSlots.Count == 4 && fixedSlots[0].Width > 0.5f, "named layout used when it has enough slots");
        List<RectangleF> fallback = Layouts.SlotsFor("Grid 2 x 2", 7, 16 / 9.0, 0);
        Chk(fallback.Count == 7, "too few slots falls back to the smart grid");
        List<RectangleF> fewer = Layouts.SlotsFor("Grid 3 x 3", 5, 16 / 9.0, 0);
        Chk(fewer.Count == 5, "a bigger layout is used for fewer windows (first 5 slots)");

        Store.UserLayouts.Add(new LayoutDef { Name = "Mine", Slots = new List<SlotDef> { new SlotDef { X = 0, Y = 0, W = 0.5f, H = 1 }, new SlotDef { X = 0.5f, Y = 0, W = 0.5f, H = 1 } } });
        Chk(Layouts.Find("Mine") != null && Layouts.SlotsFor("Mine", 2, 16 / 9.0, 0).Count == 2, "a custom layout is found and used");
        Chk(Layouts.All().Count == Layouts.Builtin.Count + 1, "custom layouts show up in the list");
        Store.UserLayouts.Clear();
        return fails;
    }
}
