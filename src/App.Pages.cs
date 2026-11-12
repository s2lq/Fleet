using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fleet
{
    public partial class App
    {
        // ------------------------------------------------------------ Layout
        void DrawLayout(Ui ui, RectangleF a)
        {
            Title(ui, a, "Layout", "Tile the running Roblox windows on a screen.");
            float y = a.Y + 90;
            Screen[] screens = Screen.AllScreens;
            float lw = Math.Min(400, a.Width * 0.36f);
            RectangleF ctl = new RectangleF(a.X, y, lw, 520);
            ui.Glass(ctl, 18, 0);
            float px = ctl.X + 26, pw = ctl.Width - 52;

            Label(ui, "Screen", px, ctl.Y + 24);
            string[] names = screens.Select((s, i) => Lang.F("Screen {0}", i + 1)).ToArray();
            int mon = Math.Max(0, Math.Min(Store.Cfg.LayoutMonitor, screens.Length - 1));
            int nm = ui.Segmented("lay.mon", new RectangleF(px, ctl.Y + 46, pw, 40), names, mon);
            if (nm != Store.Cfg.LayoutMonitor) { Store.Cfg.LayoutMonitor = nm; Store.SaveSettings(); }

            Label(ui, "Columns", px, ctl.Y + 108);
            int ci = Math.Max(0, Math.Min(5, Store.Cfg.LayoutCols));
            int nc = ui.Segmented("lay.cols", new RectangleF(px, ctl.Y + 130, pw, 40), new string[] { "Auto", "1", "2", "3", "4", "5" }, ci);
            if (nc != ci) { Store.Cfg.LayoutCols = nc; Store.SaveSettings(); }

            Label(ui, "Space between windows", px, ctl.Y + 192);
            ui.Text(Store.Cfg.LayoutGap + " px", ctl.Right - 26 - 50, ctl.Y + 192, Fonts.SmallB, Pal.A);
            int gap = ui.Slider("lay.gap", new RectangleF(px, ctl.Y + 220, pw, 24), Store.Cfg.LayoutGap, 0, 30);
            if (gap != Store.Cfg.LayoutGap) { Store.Cfg.LayoutGap = gap; Store.SaveSettings(); }

            bool bl = Tog(ui, "lay.bl", "Borderless", "Removes the title bar.", px, ctl.Y + 266, pw, Store.Cfg.LayoutBorderless);
            if (bl != Store.Cfg.LayoutBorderless) { Store.Cfg.LayoutBorderless = bl; Store.SaveSettings(); }
            bool tm = Tog(ui, "lay.tm", "Always on top", "Keeps the windows above others.", px, ctl.Y + 318, pw, Store.Cfg.LayoutTopMost);
            if (tm != Store.Cfg.LayoutTopMost) { Store.Cfg.LayoutTopMost = tm; Store.SaveSettings(); }
            bool rn = Tog(ui, "lay.rn", "Name windows", "Shows the account name as the title.", px, ctl.Y + 370, pw, Store.Cfg.LayoutRename);
            if (rn != Store.Cfg.LayoutRename) { Store.Cfg.LayoutRename = rn; Store.SaveSettings(); }

            if (ui.Button("lay.go", new RectangleF(px, ctl.Y + 432, pw, 48), "Arrange now", Btn.Primary, "grid"))
            {
                int m = Store.Cfg.LayoutMonitor, c = Store.Cfg.LayoutCols, g = Store.Cfg.LayoutGap;
                canvas.Post(delegate { Engine.Arrange(m, c, g); });
            }

            RectangleF pv = new RectangleF(a.X + lw + 16, y, a.Width - lw - 16, 520);
            ui.Glass(pv, 18, 0);
            Screen sc = screens[Math.Max(0, Math.Min(Store.Cfg.LayoutMonitor, screens.Length - 1))];
            double aspect = sc.WorkingArea.Width / (double)sc.WorkingArea.Height;
            float maxW = pv.Width - 60, maxH = pv.Height - 90;
            float w = maxW, h = (float)(w / aspect);
            if (h > maxH) { h = maxH; w = (float)(h * aspect); }
            RectangleF mon2 = new RectangleF(pv.X + (pv.Width - w) / 2, pv.Y + 30, w, h);
            ui.Fill(mon2, 8, Pal.Panel);
            ui.Stroke(mon2, 8, Pal.Line2, 1.4f);
            int n = Math.Max(Engine.Live.Count, Math.Max(selected.Count, 1));
            int c2, rows;
            Engine.Grid(n, Store.Cfg.LayoutCols, aspect, out c2, out rows);
            float g2 = Store.Cfg.LayoutGap * (w / sc.WorkingArea.Width) * 2 + 2;
            float cw = (mon2.Width - g2 * (c2 + 1)) / c2, chh = (mon2.Height - g2 * (rows + 1)) / rows;
            for (int i = 0; i < n; i++)
            {
                RectangleF cell = new RectangleF(mon2.X + g2 + (i % c2) * (cw + g2), mon2.Y + g2 + (i / c2) * (chh + g2), cw, chh);
                bool live = i < Engine.Live.Count;
                ui.Fill(cell, 5, live ? Pal.Panel2 : Pal.Panel);
                ui.Stroke(cell, 5, live ? Pal.Line2 : Pal.Line, 1f);
                if (live) ui.TextIn(Ui.Fit(ui, Engine.Live[i].Acc.Label, Fonts.SmallB, cell.Width - 8), cell, Fonts.SmallB, Pal.Text, StringAlignment.Center);
            }
            ui.TextIn(Engine.Live.Count > 0 ? Lang.F("{0} window(s) running", Engine.Live.Count) : Lang.F("Preview with {0} window(s)", n), new RectangleF(pv.X, pv.Bottom - 50, pv.Width, 30), Fonts.Small, Pal.Muted, StringAlignment.Center);
        }

        // ------------------------------------------------------------ Performance
        void DrawPerformance(Ui ui, RectangleF a)
        {
            Title(ui, a, "Performance", "Keep every Roblox window light on your PC.");
            RectangleF view = new RectangleF(a.X - 8, a.Y + 82, a.Width + 16, a.Height - 82);
            ui.BeginScroll(view, 900, Scroll("perf"));
            float y = view.Y + 8;
            float lw = Math.Min(480, a.Width * 0.44f);
            RectangleF ctl = new RectangleF(a.X, y, lw, 850);
            ui.Glass(ctl, 18, 0);
            float px = ctl.X + 26, pw = ctl.Width - 52;
            Settings c = Store.Cfg;

            Label(ui, "Frame rate limit", px, ctl.Y + 24);
            int[] caps = { 0, 15, 30, 60, 120, 240 };
            int ci = Math.Max(0, Array.IndexOf(caps, c.FpsCap));
            int nci = ui.Segmented("pf.fps", new RectangleF(px, ctl.Y + 46, pw, 40), new string[] { "Off", "15", "30", "60", "120", "240" }, ci);
            if (nci != ci) { c.FpsCap = caps[nci]; Store.SaveSettings(); Engine.SetFpsCap(c.FpsCap); }

            Label(ui, "Graphics", px, ctl.Y + 108);
            int gi = Math.Max(0, Array.IndexOf(Perf.GfxNames, c.GfxPreset));
            int ngi = ui.Picker("pf.gfx", new RectangleF(px, ctl.Y + 130, pw, 42), Perf.GfxNames, gi, "Graphics");
            if (ngi != gi) { c.GfxPreset = Perf.GfxNames[ngi]; Store.SaveSettings(); Perf.SetGraphics(c.GfxPreset); }
            ui.Text("Applied to windows you start afterwards.", px, ctl.Y + 178, Fonts.Small, Pal.Faint);

            Label(ui, "Process priority", px, ctl.Y + 212);
            string[] pri = { "Normal", "BelowNormal", "Idle" };
            int pi = Math.Max(0, Array.IndexOf(pri, c.Priority));
            int npi = ui.Segmented("pf.pri", new RectangleF(px, ctl.Y + 234, pw, 40), new string[] { "Normal", "Lower", "Lowest" }, pi);
            if (npi != pi) { c.Priority = pri[npi]; Store.SaveSettings(); Engine.ApplyPriorityAll(); }

            Label(ui, "CPU limit per window", px, ctl.Y + 300);
            string[] cm = { "Off", "Each", "Total" };
            int cmi = Math.Max(0, Array.IndexOf(cm, c.CpuCapMode));
            int ncm = ui.Segmented("pf.cpu", new RectangleF(px, ctl.Y + 322, pw, 40), new string[] { "Off", "Each window", "All together" }, cmi);
            if (ncm != cmi) { c.CpuCapMode = cm[ncm]; Store.SaveSettings(); Perf.CapChanged(); Perf.Apply(); }
            if (c.CpuCapMode != "Off")
            {
                ui.Text(c.CpuCapPercent + " % of the whole PC", px, ctl.Y + 372, Fonts.Small, Pal.A);
                int pc = ui.Slider("pf.cpup", new RectangleF(px, ctl.Y + 392, pw, 24), c.CpuCapPercent, 1, 100);
                if (pc != c.CpuCapPercent) { c.CpuCapPercent = pc; Store.SaveSettings(); Perf.CapChanged(); }
            }

            Label(ui, "Memory limit per window", px, ctl.Y + 440);
            string[] rm = { "Off", "Auto250", "Fixed500" };
            int rmi = Math.Max(0, Array.IndexOf(rm, c.RamCapMode));
            int nrm = ui.Segmented("pf.ram", new RectangleF(px, ctl.Y + 462, pw, 40), new string[] { "Off", "250 MB", "500 MB" }, rmi);
            if (nrm != rmi) { c.RamCapMode = rm[nrm]; Store.SaveSettings(); Perf.Apply(); }

            bool tb = Tog(ui, "pf.bg", "Slow background windows", "Only the window you are using runs at full speed.", px, ctl.Y + 530, pw, c.ThrottleBackground);
            if (tb != c.ThrottleBackground) { c.ThrottleBackground = tb; Store.SaveSettings(); if (!tb) Engine.ApplyPriorityAll(); }
            bool af = Tog(ui, "pf.afk", "Anti-AFK", Lang.F("Sends a harmless key every {0} min so Roblox does not kick idle windows.", c.AntiAfkMin), px, ctl.Y + 590, pw, c.AntiAfk);
            if (af != c.AntiAfk) { c.AntiAfk = af; Store.SaveSettings(); }
            if (c.AntiAfk)
            {
                int am = ui.Slider("pf.afkm", new RectangleF(px, ctl.Y + 640, pw, 24), c.AntiAfkMin, 2, 19);
                if (am != c.AntiAfkMin) { c.AntiAfkMin = am; Store.SaveSettings(); }
            }
            bool at = Tog(ui, "pf.auto", "Trim memory automatically", Lang.F("Every {0} minutes.", c.TrimEveryMin), px, ctl.Y + 690, pw, c.AutoTrim);
            if (at != c.AutoTrim) { c.AutoTrim = at; Store.SaveSettings(); }
            int te = ui.Slider("pf.every", new RectangleF(px, ctl.Y + 738, pw, 24), c.TrimEveryMin, 1, 60);
            if (te != c.TrimEveryMin) { c.TrimEveryMin = te; Store.SaveSettings(); }
            if (ui.Button("pf.trim", new RectangleF(px, ctl.Y + 780, pw, 46), "Trim memory now", Btn.Primary, "bolt")) canvas.Post(delegate { Engine.TrimMemory(); });

            RectangleF live = new RectangleF(a.X + lw + 16, y, a.Width - lw - 16, 440);
            ui.Glass(live, 18, 0);
            ui.Text("Live usage", live.X + 26, live.Y + 22, Fonts.H2, Pal.Text);
            if (Engine.Live.Count == 0)
                ui.TextIn("Start some accounts and their CPU and memory show here.", live.Inset(20), Fonts.Body, Pal.Muted, StringAlignment.Center);
            else
            {
                float ry = live.Y + 66;
                long maxRam = Math.Max(1L << 29, Engine.Live.Max(r => r.Ram));
                foreach (Running r in Engine.Live.Take(7))
                {
                    ui.Text(Ui.Fit(ui, r.Acc.Label, Fonts.BodyB, 150), live.X + 26, ry, Fonts.BodyB, Pal.Text);
                    float bx = live.X + 190, bw = (live.Width - 216 - 24) / 2;
                    ui.Bar(new RectangleF(bx, ry + 6, bw, 8), (float)(r.Cpu / 30.0), Pal.A, Pal.A);
                    ui.Text(r.Cpu.ToString("0.0") + "% CPU", bx, ry + 20, Fonts.Small, Pal.Muted);
                    ui.Bar(new RectangleF(bx + bw + 24, ry + 6, bw, 8), r.Ram / (float)maxRam, Pal.A, Pal.A);
                    ui.Text(Mem(r.Ram), bx + bw + 24, ry + 20, Fonts.Small, Pal.Muted);
                    ry += 52;
                }
            }
            ui.EndScroll();
        }
    }

    static class RectExt
    {
        public static RectangleF Inset(this RectangleF r, float d) { return new RectangleF(r.X + d, r.Y + d, r.Width - d * 2, r.Height - d * 2); }
    }
}
