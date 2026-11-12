using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fleet
{
    public static partial class Engine
    {
        static string arrangedSig = "", pendingSig = "";
        static int stable;

        // the smallest size Roblox has actually accepted so far. It starts at nothing: windows are asked for whatever size fits,
        // and only if a window refuses to get that small is the smallest size remembered and the layout done again.
        public static Size LearnedMin = Size.Empty;

        // the running clients that have a visible window, in account order
        static List<KeyValuePair<Running, IntPtr>> VisibleWindows()
        {
            List<KeyValuePair<Running, IntPtr>> list = new List<KeyValuePair<Running, IntPtr>>();
            if (Live.Count == 0) return list;
            Dictionary<uint, IntPtr> wins = Native.WindowsOf(new HashSet<uint>(Live.Select(r => (uint)r.Pid)));
            foreach (Running r in Live.OrderBy(x => x.Acc.Order))
            {
                IntPtr h;
                if (wins.TryGetValue((uint)r.Pid, out h)) list.Add(new KeyValuePair<Running, IntPtr>(r, h));
            }
            return list;
        }

        public static List<Screen> TargetScreens()
        {
            Screen[] all = Screen.AllScreens;
            if (Store.Cfg.LayoutAllScreens) return all.OrderBy(s => !s.Primary).ThenBy(s => s.Bounds.X).ToList();
            return new List<Screen> { all[Math.Max(0, Math.Min(Store.Cfg.LayoutMonitor, all.Length - 1))] };
        }

        // how many windows each screen gets, by its size (largest remainder, so the total is exact)
        public static int[] Spread(int n, double[] areas)
        {
            double total = areas.Sum();
            int[] counts = new int[areas.Length];
            double[] frac = new double[areas.Length];
            int used = 0;
            for (int i = 0; i < areas.Length; i++) { double q = n * areas[i] / total; counts[i] = (int)Math.Floor(q); frac[i] = q - counts[i]; used += counts[i]; }
            while (used < n)
            {
                int best = 0;
                for (int i = 1; i < areas.Length; i++) if (frac[i] > frac[best]) best = i;
                counts[best]++; frac[best] = -1; used++;
            }
            return counts;
        }

        // no screen gets more than it can hold at the smallest size; the rest moves to screens with room
        public static int[] Fit(int[] counts, int[] caps)
        {
            int[] c = (int[])counts.Clone();
            int extra = 0;
            for (int i = 0; i < c.Length; i++) if (c[i] > caps[i]) { extra += c[i] - caps[i]; c[i] = caps[i]; }
            while (extra > 0)
            {
                int best = -1, room = 0;
                for (int i = 0; i < c.Length; i++) if (caps[i] - c[i] > room) { room = caps[i] - c[i]; best = i; }
                if (best < 0) { int big = Array.IndexOf(caps, caps.Max()); c[big] += extra; break; }     // nowhere left: they will overlap
                int give = Math.Min(room, extra);
                c[best] += give; extra -= give;
            }
            return c;
        }

        // the layout for every window: which screen, which slot
        public static List<Rectangle> PlanFor(int n, List<Screen> screens, Size min)
        {
            List<Rectangle> areas = screens.Select(s => s.WorkingArea).ToList();
            int[] counts = Spread(n, areas.Select(a => (double)a.Width * a.Height).ToArray());
            if (min.Width > 0 || min.Height > 0) counts = Fit(counts, areas.Select(a => Math.Max(1, Layouts.MaxFit(a, min))).ToArray());
            List<Rectangle> plan = new List<Rectangle>();
            for (int k = 0; k < areas.Count; k++) if (counts[k] > 0) plan.AddRange(Layouts.Slots(counts[k], areas[k], min));
            return plan;
        }

        // moves one window so that what you SEE of it fills the slot exactly
        static void Place(IntPtr hw, Rectangle slot)
        {
            if (Native.IsIconic(hw) || Native.IsZoomed(hw)) Native.ShowWindow(hw, 9);
            if (Store.Cfg.LayoutBorderless) Native.SetWindowLong(hw, -16, Native.GetWindowLong(hw, -16) & ~(0xC00000 | 0x40000));
            IntPtr after = Store.Cfg.LayoutTopMost ? (IntPtr)(-1) : IntPtr.Zero;
            uint flags = (Store.Cfg.LayoutTopMost ? 0u : Native.SWP_NOZORDER) | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW | Native.SWP_FRAMECHANGED | Native.SWP_NOSENDCHANGING;
            // the invisible border Windows puts around a window would make neighbours overlap, so it is measured and added to the size
            for (int pass = 0; pass < 3; pass++)
            {
                Rectangle outer = Native.OuterRect(hw), vis = Native.VisibleRect(hw);
                int l = vis.Left - outer.Left, t = vis.Top - outer.Top, r = outer.Right - vis.Right, b = outer.Bottom - vis.Bottom;
                if (pass > 0 && Math.Abs(vis.X - slot.X) <= 1 && Math.Abs(vis.Y - slot.Y) <= 1 && Math.Abs(vis.Width - slot.Width) <= 1 && Math.Abs(vis.Height - slot.Height) <= 1) break;
                // Native.SetWindowPos with NOSENDCHANGING: the client cannot veto the size, so Roblox's own minimum does not apply
                Native.SetWindowPos(hw, after, slot.X - l, slot.Y - t, slot.Width + l + r, slot.Height + t + b, flags);
            }
        }

        // puts every visible Roblox window in its place, by the chosen screens
        public static int ArrangeNow(bool log)
        {
            List<KeyValuePair<Running, IntPtr>> vis = VisibleWindows();
            if (vis.Count == 0) { if (log) Log.Add(Live.Count == 0 ? "No Roblox window to arrange." : "The Roblox windows are not visible yet."); return 0; }
            List<Screen> screens = TargetScreens();
            List<Rectangle> plan = null;
            for (int round = 0; round < 3; round++)
            {
                plan = PlanFor(vis.Count, screens, LearnedMin);
                for (int i = 0; i < vis.Count; i++)
                {
                    IntPtr hw = vis[i].Value;
                    if (Store.Cfg.LayoutRename) Native.SetWindowText(hw, vis[i].Key.Acc.Label);
                    Place(hw, plan[Math.Min(i, plan.Count - 1)]);
                }
                // did every window really get that small? if one refused, that is Roblox's smallest size: remember it and plan again
                Size grew = LearnedMin;
                for (int i = 0; i < vis.Count; i++)
                {
                    Rectangle want = plan[Math.Min(i, plan.Count - 1)], got = Native.VisibleRect(vis[i].Value);
                    if (got.Width > want.Width + 2) grew.Width = Math.Max(grew.Width, got.Width);
                    if (got.Height > want.Height + 2) grew.Height = Math.Max(grew.Height, got.Height);
                }
                if (grew == LearnedMin) break;
                LearnedMin = grew;
            }
            int overlaps = CountOverlaps(vis);
            if (log) Log.Add(overlaps == 0 ? "Arranged " + vis.Count + " window(s)." : "Arranged " + vis.Count + " window(s), but " + overlaps + " overlap: Roblox does not go smaller than " + LearnedMin.Width + " x " + LearnedMin.Height + ". Use another screen or fewer windows.");
            arrangedSig = Signature(vis); pendingSig = arrangedSig; stable = 0;
            return vis.Count;
        }

        // how many pairs of windows cover each other by more than a few pixels
        static int CountOverlaps(List<KeyValuePair<Running, IntPtr>> vis)
        {
            List<Rectangle> rects = vis.Select(v => Native.VisibleRect(v.Value)).ToList();
            int n = 0;
            for (int i = 0; i < rects.Count; i++)
                for (int j = i + 1; j < rects.Count; j++)
                {
                    Rectangle x = Rectangle.Intersect(rects[i], rects[j]);
                    if (x.Width > 4 && x.Height > 4) n++;
                }
            return n;
        }

        static string Signature(List<KeyValuePair<Running, IntPtr>> vis)
        {
            return string.Join(",", vis.Select(k => k.Key.Pid + ":" + k.Value.ToInt64()).ToArray());
        }

        // called every second: when the set of windows has changed and then settled, arrange them again
        public static void AutoArrangeTick()
        {
            if (!Store.Cfg.AutoArrange || Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            List<KeyValuePair<Running, IntPtr>> vis = VisibleWindows();
            if (vis.Count == 0) { arrangedSig = pendingSig = ""; stable = 0; return; }
            string sig = Signature(vis);
            if (sig == arrangedSig) { stable = 0; return; }
            if (sig == pendingSig) stable++; else { pendingSig = sig; stable = 0; }
            if (stable >= 2) ArrangeNow(false);
        }
    }
}
