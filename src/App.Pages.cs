using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fleet
{
    public partial class App
    {
        // ------------------------------------------------------------ Performance
        void DrawPerformance(Ui ui, RectangleF a)
        {
            Title(ui, a, "Performance", "Keep every Roblox window light on your PC.");
            RectangleF view = new RectangleF(a.X - 8, a.Y + 82, a.Width + 16, a.Height - 82);
            Scroll("perf").Auto = true;
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
                    ui.Bar(new RectangleF(bx, ry + 6, bw, 8), ui.Anim("bc" + r.Acc.Id, (float)(r.Cpu / 30.0), 0.15f), Pal.A, Pal.A);
                    ui.Text(r.Cpu.ToString("0.0") + "% CPU", bx, ry + 20, Fonts.Small, Pal.Muted);
                    ui.Bar(new RectangleF(bx + bw + 24, ry + 6, bw, 8), ui.Anim("br" + r.Acc.Id, r.Ram / (float)maxRam, 0.15f), Pal.A, Pal.A);
                    ui.Text(Mem(r.Ram), bx + bw + 24, ry + 20, Fonts.Small, Pal.Muted);
                    ry += 52;
                }
            }

            // whole-PC memory
            RectangleF sm = new RectangleF(a.X + lw + 16, y + 456, a.Width - lw - 16, 330);
            ui.Glass(sm, 18, 0);
            ui.Text("System memory", sm.X + 26, sm.Y + 22, Fonts.H2, Pal.Text);
            int load; long total, avail;
            if (MemClean.Status(out load, out total, out avail))
            {
                ui.Text(Lang.F("{0}% used, {1} free of {2}", load, Mem(avail), Mem(total)), sm.X + 26, sm.Y + 58, Fonts.Body, Pal.Muted);
                ui.Bar(new RectangleF(sm.X + 26, sm.Y + 90, sm.Width - 52, 10), ui.Anim("sysmem", load / 100f, 0.15f), load > 85 ? Pal.Bad : Pal.A, Pal.A);
            }
            bool admin = MemClean.IsAdmin();
            if (ui.Button("sm.clean", new RectangleF(sm.X + 26, sm.Y + 122, 220, 44), "Clean memory now", Btn.Primary, "bolt")) CleanMemoryNow();
            ui.Text(admin ? "Running as administrator." : "Windows asks for administrator rights for each clean.", sm.X + 262, sm.Y + 136, Fonts.Small, Pal.Faint);
            bool sa = Tog(ui, "sm.auto", "Clean automatically", Lang.F("When memory use is above {0}%. Needs Fleet to run as administrator.", c.SysMemThreshold), sm.X + 26, sm.Y + 190, sm.Width - 52, c.SysMemAuto);
            if (sa != c.SysMemAuto) { c.SysMemAuto = sa; Store.SaveSettings(); }
            int st = ui.Slider("sm.thr", new RectangleF(sm.X + 26, sm.Y + 236, sm.Width - 52, 24), c.SysMemThreshold, 50, 95);
            if (st != c.SysMemThreshold) { c.SysMemThreshold = st; Store.SaveSettings(); }
            if (!admin && ui.Button("sm.admin", new RectangleF(sm.X + 26, sm.Y + 274, 260, 40), "Restart as administrator", Btn.Ghost, null)) MemClean.RestartAsAdmin();
            ui.EndScroll();
        }

        DateTime lastClean = DateTime.MinValue;

        void CleanMemoryNow()
        {
            canvas.Post(delegate
            {
                int l0; long t0, a0; MemClean.Status(out l0, out t0, out a0);
                string err = MemClean.IsAdmin() ? MemClean.Clean() : MemClean.CleanElevated();
                int l1; long t1, a1; MemClean.Status(out l1, out t1, out a1);
                Log.Add(err != null ? "Memory clean: " + err : "Memory cleaned: " + Mem(Math.Max(0, a1 - a0)) + " freed.");
                lastClean = DateTime.Now;
            });
        }

        void AutoCleanMemory()
        {
            int load; long total, avail;
            if (!MemClean.Status(out load, out total, out avail) || load < Store.Cfg.SysMemThreshold) return;
            if ((DateTime.Now - lastClean).TotalSeconds < 90 || !MemClean.IsAdmin()) return;
            lastClean = DateTime.Now;
            string err = MemClean.Clean();
            Log.Add(err != null ? "Memory clean: " + err : "Memory was at " + load + "%, cleaned automatically.");
        }
    }

    static class RectExt
    {
        public static RectangleF Inset(this RectangleF r, float d) { return new RectangleF(r.X + d, r.Y + d, r.Width - d * 2, r.Height - d * 2); }
    }
}
