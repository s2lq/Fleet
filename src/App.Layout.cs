using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fleet
{
    public partial class App
    {
        void DrawLayout(Ui ui, RectangleF a)
        {
            Title(ui, a, "Layout", "The Roblox windows arrange themselves to fill the screen.");
            Settings c = Store.Cfg;
            Screen[] screens = Screen.AllScreens;
            float y = a.Y + 90;
            float lw = Math.Min(420, a.Width * 0.38f);
            RectangleF ctl = new RectangleF(a.X, y, lw, 500);
            ui.Glass(ctl, 18, 0);
            float px = ctl.X + 26, pw = ctl.Width - 52, cy = ctl.Y + 22;

            Label(ui, "Screens", px, cy);
            int sm = ui.Segmented("lay.scr", new RectangleF(px, cy + 22, pw, 40), new string[] { "This screen", "All screens" }, c.LayoutAllScreens ? 1 : 0);
            if ((sm == 1) != c.LayoutAllScreens) { c.LayoutAllScreens = sm == 1; Store.SaveSettings(); }
            cy += 78;
            if (!c.LayoutAllScreens && screens.Length > 1)
            {
                string[] names = screens.Select((s, i) => Lang.F("Screen {0}", i + 1)).ToArray();
                int mon = Math.Max(0, Math.Min(c.LayoutMonitor, screens.Length - 1));
                int nm = ui.Segmented("lay.mon", new RectangleF(px, cy, pw, 40), names, mon);
                if (nm != c.LayoutMonitor) { c.LayoutMonitor = nm; Store.SaveSettings(); }
                cy += 58;
            }
            bool aa = Tog(ui, "lay.auto", "Rearrange automatically", "Fits the windows again when one opens or closes.", px, cy, pw, c.AutoArrange);
            if (aa != c.AutoArrange) { c.AutoArrange = aa; Store.SaveSettings(); } cy += 54;
            bool bl = Tog(ui, "lay.bl", "Borderless", "Removes the title bar.", px, cy, pw, c.LayoutBorderless);
            if (bl != c.LayoutBorderless) { c.LayoutBorderless = bl; Store.SaveSettings(); } cy += 54;
            bool tm = Tog(ui, "lay.tm", "Always on top", "Keeps the windows above others.", px, cy, pw, c.LayoutTopMost);
            if (tm != c.LayoutTopMost) { c.LayoutTopMost = tm; Store.SaveSettings(); } cy += 54;
            bool rn = Tog(ui, "lay.rn", "Name windows", "Shows the account name as the title.", px, cy, pw, c.LayoutRename);
            if (rn != c.LayoutRename) { c.LayoutRename = rn; Store.SaveSettings(); } cy += 64;
            if (ui.Button("lay.go", new RectangleF(px, cy, pw, 48), "Arrange now", Btn.Primary, "grid")) canvas.Post(delegate { Engine.ArrangeNow(true); });

            // preview of every target screen
            RectangleF pv = new RectangleF(a.X + lw + 16, y, a.Width - lw - 16, ctl.Height);
            ui.Glass(pv, 18, 0);
            List<Screen> targets = c.LayoutAllScreens ? screens.OrderBy(s => !s.Primary).ThenBy(s => s.Bounds.X).ToList() : new List<Screen> { screens[Math.Max(0, Math.Min(c.LayoutMonitor, screens.Length - 1))] };
            int n = Math.Max(Engine.Live.Count, Math.Max(selected.Count, 1));
            if (Engine.Live.Count == 0 && selected.Count == 0) n = 4;
            List<Rectangle> plan = Engine.PlanFor(n, targets, Engine.LearnedMin);
            int minX = targets.Min(s => s.Bounds.X), minY = targets.Min(s => s.Bounds.Y), maxX = targets.Max(s => s.Bounds.Right), maxY = targets.Max(s => s.Bounds.Bottom);
            float scale = Math.Min((pv.Width - 60) / (maxX - minX), (pv.Height - 110) / (maxY - minY));
            float ox = pv.X + (pv.Width - (maxX - minX) * scale) / 2, oy = pv.Y + 36;
            int idx = 0;
            for (int k = 0; k < targets.Count; k++)
            {
                Rectangle b = targets[k].WorkingArea;
                RectangleF mr = new RectangleF(ox + (b.X - minX) * scale, oy + (b.Y - minY) * scale, b.Width * scale, b.Height * scale);
                ui.Fill(mr, 4, Pal.Panel);
                {
                    List<Rectangle> mine = plan.Where(p => b.Contains(new Point(p.X + p.Width / 2, p.Y + p.Height / 2))).ToList();
                    for (int i = 0; i < mine.Count; i++, idx++)
                    {
                        Rectangle s = mine[i];
                        RectangleF cell = new RectangleF(ox + (s.X - minX) * scale, oy + (s.Y - minY) * scale, s.Width * scale, s.Height * scale);
                        bool live = idx < Engine.Live.Count;
                        ui.Fill(cell, 0, live ? Pal.Panel2 : Pal.Bg);
                        ui.Stroke(cell, 0, Pal.Line2, 1f);
                        string label = live ? Engine.Live[idx].Acc.Label : (idx + 1).ToString();
                        ui.TextIn(Ui.Fit(ui, label, Fonts.SmallB, cell.Width - 6), cell, Fonts.SmallB, live ? Pal.Text : Pal.Faint, StringAlignment.Center);
                    }
                }
                ui.Stroke(mr, 4, Pal.Line2, 1.4f);
            }
            ui.TextIn(Engine.Live.Count > 0 ? Lang.F("{0} window(s) running", Engine.Live.Count) : Lang.F("Preview with {0} window(s)", n), new RectangleF(pv.X, pv.Bottom - 56, pv.Width, 30), Fonts.Small, Pal.Muted, StringAlignment.Center);
            if (Engine.LearnedMin.Width > 0) ui.TextIn(Lang.F("Roblox would not go smaller than {0} x {1}", Engine.LearnedMin.Width, Engine.LearnedMin.Height), new RectangleF(pv.X, pv.Bottom - 32, pv.Width, 26), Fonts.Small, Pal.Warn, StringAlignment.Center);
        }
    }
}
