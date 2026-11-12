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

        static List<Screen> TargetScreens()
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

        // puts every visible Roblox window in its place, by the chosen layout and across the chosen screens
        public static int ArrangeNow(bool log)
        {
            List<KeyValuePair<Running, IntPtr>> vis = VisibleWindows();
            if (vis.Count == 0) { if (log) Log.Add(Live.Count == 0 ? "No Roblox window to arrange." : "The Roblox windows are not visible yet."); return 0; }
            List<Screen> screens = TargetScreens();
            int[] counts = Spread(vis.Count, screens.Select(s => (double)s.WorkingArea.Width * s.WorkingArea.Height).ToArray());
            const int gap = 0;      // windows sit edge to edge
            int idx = 0;
            for (int k = 0; k < screens.Count; k++)
            {
                int n = counts[k];
                if (n == 0) continue;
                Rectangle area = screens[k].WorkingArea;
                List<RectangleF> slots = Layouts.Smart(n, area.Width / (double)area.Height);
                for (int i = 0; i < n; i++, idx++)
                {
                    RectangleF s = slots[Math.Min(i, slots.Count - 1)];
                    int x = area.X + (int)Math.Round(s.X * area.Width) + gap / 2, y = area.Y + (int)Math.Round(s.Y * area.Height) + gap / 2;
                    int w = Math.Max(120, (int)Math.Round(s.Width * area.Width) - gap), h = Math.Max(90, (int)Math.Round(s.Height * area.Height) - gap);
                    IntPtr hw = vis[idx].Value;
                    Native.ShowWindow(hw, 9);
                    if (Store.Cfg.LayoutBorderless) Native.SetWindowLong(hw, -16, Native.GetWindowLong(hw, -16) & ~(0xC00000 | 0x40000));
                    if (Store.Cfg.LayoutRename) Native.SetWindowText(hw, vis[idx].Key.Acc.Label);
                    Native.SetWindowPos(hw, Store.Cfg.LayoutTopMost ? (IntPtr)(-1) : IntPtr.Zero, x, y, w, h, (Store.Cfg.LayoutTopMost ? 0u : Native.SWP_NOZORDER) | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW | 0x20);
                }
            }
            if (log) Log.Add("Arranged " + vis.Count + " window(s).");
            arrangedSig = Signature(vis); pendingSig = arrangedSig; stable = 0;
            return vis.Count;
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
