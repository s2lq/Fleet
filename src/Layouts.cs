using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Web.Script.Serialization;

namespace Fleet
{
    public class SlotDef { public float X, Y, W, H; }

    // a window layout: where each window goes, as fractions of a screen (0 to 1)
    public class LayoutDef
    {
        public string Name = "";
        public List<SlotDef> Slots = new List<SlotDef>();
        [ScriptIgnore] public bool Builtin;
        public LayoutDef Copy() { LayoutDef d = new LayoutDef { Name = Name }; foreach (SlotDef s in Slots) d.Slots.Add(new SlotDef { X = s.X, Y = s.Y, W = s.W, H = s.H }); return d; }
    }

    public static class Layouts
    {
        public const string Auto = "Auto (smart grid)";
        public static readonly List<LayoutDef> Builtin = Build();

        static LayoutDef L(string name, params float[][] r)
        {
            LayoutDef d = new LayoutDef { Name = name, Builtin = true };
            foreach (float[] a in r) d.Slots.Add(new SlotDef { X = a[0], Y = a[1], W = a[2], H = a[3] });
            return d;
        }

        static LayoutDef Grid(int c, int r)
        {
            LayoutDef d = new LayoutDef { Name = "Grid " + c + " x " + r, Builtin = true };
            for (int y = 0; y < r; y++) for (int x = 0; x < c; x++) d.Slots.Add(new SlotDef { X = x / (float)c, Y = y / (float)r, W = 1f / c, H = 1f / r });
            return d;
        }

        // one big window on the left, k small ones stacked on the right
        static LayoutDef MainSide(int k, float big)
        {
            LayoutDef d = new LayoutDef { Name = "Main left + " + k + " side", Builtin = true };
            d.Slots.Add(new SlotDef { X = 0, Y = 0, W = big, H = 1 });
            for (int i = 0; i < k; i++) d.Slots.Add(new SlotDef { X = big, Y = i / (float)k, W = 1 - big, H = 1f / k });
            return d;
        }

        // one big window on top, k small ones in a strip at the bottom
        static LayoutDef MainTop(int k, float big)
        {
            LayoutDef d = new LayoutDef { Name = "Main top + " + k + " strip", Builtin = true };
            d.Slots.Add(new SlotDef { X = 0, Y = 0, W = 1, H = big });
            for (int i = 0; i < k; i++) d.Slots.Add(new SlotDef { X = i / (float)k, Y = big, W = 1f / k, H = 1 - big });
            return d;
        }

        static LayoutDef Cascade(int n)
        {
            LayoutDef d = new LayoutDef { Name = "Cascade " + n, Builtin = true };
            float step = 0.5f / Math.Max(1, n - 1), size = 0.5f;
            for (int i = 0; i < n; i++) d.Slots.Add(new SlotDef { X = i * step, Y = i * step, W = size, H = size });
            return d;
        }

        static List<LayoutDef> Build()
        {
            List<LayoutDef> l = new List<LayoutDef>();
            int[][] grids = { new[] { 1, 1 }, new[] { 2, 1 }, new[] { 1, 2 }, new[] { 3, 1 }, new[] { 1, 3 }, new[] { 2, 2 }, new[] { 3, 2 }, new[] { 2, 3 }, new[] { 4, 1 }, new[] { 3, 3 }, new[] { 4, 2 }, new[] { 2, 4 }, new[] { 5, 2 }, new[] { 4, 3 }, new[] { 3, 4 }, new[] { 6, 2 }, new[] { 5, 3 }, new[] { 4, 4 }, new[] { 6, 3 }, new[] { 5, 4 }, new[] { 5, 5 }, new[] { 6, 4 }, new[] { 8, 3 } };
            foreach (int[] g in grids) l.Add(Grid(g[0], g[1]));
            for (int k = 2; k <= 7; k++) l.Add(MainSide(k, k <= 3 ? 0.62f : 0.55f));
            for (int k = 3; k <= 7; k++) l.Add(MainTop(k, k <= 4 ? 0.68f : 0.6f));
            l.Add(L("Two big + 4 small", new float[] { 0, 0, 0.5f, 0.7f }, new float[] { 0.5f, 0, 0.5f, 0.7f }, new float[] { 0, 0.7f, 0.25f, 0.3f }, new float[] { 0.25f, 0.7f, 0.25f, 0.3f }, new float[] { 0.5f, 0.7f, 0.25f, 0.3f }, new float[] { 0.75f, 0.7f, 0.25f, 0.3f }));
            l.Add(L("Big middle + 4 corners", new float[] { 0.25f, 0.2f, 0.5f, 0.6f }, new float[] { 0, 0, 0.25f, 0.5f }, new float[] { 0.75f, 0, 0.25f, 0.5f }, new float[] { 0, 0.5f, 0.25f, 0.5f }, new float[] { 0.75f, 0.5f, 0.25f, 0.5f }));
            l.Add(L("Four corners", new float[] { 0, 0, 0.4f, 0.4f }, new float[] { 0.6f, 0, 0.4f, 0.4f }, new float[] { 0, 0.6f, 0.4f, 0.4f }, new float[] { 0.6f, 0.6f, 0.4f, 0.4f }));
            l.Add(L("Triple wide rows", new float[] { 0, 0, 1, 0.34f }, new float[] { 0, 0.33f, 1, 0.34f }, new float[] { 0, 0.66f, 1, 0.34f }));
            l.Add(L("Left column + 2 right", new float[] { 0, 0, 0.5f, 1 }, new float[] { 0.5f, 0, 0.5f, 0.5f }, new float[] { 0.5f, 0.5f, 0.5f, 0.5f }));
            l.Add(L("Thirds: 1 wide + 2", new float[] { 0, 0, 1, 0.6f }, new float[] { 0, 0.6f, 0.5f, 0.4f }, new float[] { 0.5f, 0.6f, 0.5f, 0.4f }));
            l.Add(L("Pyramid 1 + 2 + 3", new float[] { 0.25f, 0, 0.5f, 0.34f }, new float[] { 0, 0.34f, 0.5f, 0.33f }, new float[] { 0.5f, 0.34f, 0.5f, 0.33f }, new float[] { 0, 0.67f, 0.34f, 0.33f }, new float[] { 0.33f, 0.67f, 0.34f, 0.33f }, new float[] { 0.66f, 0.67f, 0.34f, 0.33f }));
            for (int n = 3; n <= 6; n += 1) if (n != 4 && n != 6) l.Add(Cascade(n));
            l.Add(L("Everything on top of each other", new float[] { 0, 0, 1, 1 }));
            return l;
        }

        public static List<LayoutDef> All() { return Builtin.Concat(Store.UserLayouts).ToList(); }
        public static LayoutDef Find(string name) { return All().FirstOrDefault(d => d.Name == name); }

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

        // the slots for n windows: the smart grid, or the chosen layout when it has enough slots
        public static List<RectangleF> SlotsFor(string name, int n, double aspect, int cols)
        {
            LayoutDef d = (name == null || name == Auto) ? null : Find(name);
            List<RectangleF> list = new List<RectangleF>();
            if (d != null && d.Slots.Count >= n) { foreach (SlotDef s in d.Slots.Take(Math.Max(1, n))) list.Add(new RectangleF(s.X, s.Y, s.W, s.H)); return list; }
            int c, r;
            BestGrid(n, aspect, cols, out c, out r);
            // the last row may be short: its windows get wider, so the screen stays full
            for (int i = 0; i < n; i++)
            {
                int row = i / c, inRow = Math.Min(c, n - row * c), col = i % c;
                list.Add(new RectangleF(col / (float)inRow, row / (float)r, 1f / inRow, 1f / r));
            }
            return list;
        }
    }
}
