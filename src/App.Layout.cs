using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fleet
{
    public partial class App
    {
        int layTab;
        // layout editor
        List<SlotDef> edSlots = new List<SlotDef>();
        int edSel = -1;
        string edName = "";
        bool edDrag;
        PointF edStart, edNow;
        const float Snap = 24f;

        void DrawLayout(Ui ui, RectangleF a)
        {
            Title(ui, a, "Layout", "Where the Roblox windows go, and how they rearrange themselves.");
            layTab = ui.Segmented("lay.tab", new RectangleF(a.Right - 320, a.Y + 6, 320, 40), new string[] { "Arrange", "Layouts" }, layTab);
            if (layTab == 0) DrawArrange(ui, a); else DrawLayoutLibrary(ui, a);
        }

        // ------------------------------------------------------------ arrange
        void DrawArrange(Ui ui, RectangleF a)
        {
            Settings c = Store.Cfg;
            Screen[] screens = Screen.AllScreens;
            float y = a.Y + 90;
            float lw = Math.Min(420, a.Width * 0.38f);
            RectangleF ctl = new RectangleF(a.X, y, lw, a.Height - 100);
            ui.Glass(ctl, 18, 0);
            ui.BeginScroll(new RectangleF(ctl.X, ctl.Y + 4, ctl.Width, ctl.Height - 8), 640, Scroll("laycfg"));
            float px = ctl.X + 26, pw = ctl.Width - 52, cy = ctl.Y + 18;

            Label(ui, "Screens", px, cy);
            int sm = ui.Segmented("lay.scr", new RectangleF(px, cy + 22, pw, 40), new string[] { "This screen", "All screens" }, c.LayoutAllScreens ? 1 : 0);
            if ((sm == 1) != c.LayoutAllScreens) { c.LayoutAllScreens = sm == 1; Store.SaveSettings(); }
            cy += 76;
            if (!c.LayoutAllScreens && screens.Length > 1)
            {
                string[] names = screens.Select((s, i) => Lang.F("Screen {0}", i + 1)).ToArray();
                int mon = Math.Max(0, Math.Min(c.LayoutMonitor, screens.Length - 1));
                int nm = ui.Segmented("lay.mon", new RectangleF(px, cy, pw, 40), names, mon);
                if (nm != c.LayoutMonitor) { c.LayoutMonitor = nm; Store.SaveSettings(); }
                cy += 54;
            }

            Label(ui, "Layout", px, cy);
            List<string> layoutNames = new List<string> { Layouts.Auto }.Concat(Layouts.All().Select(d => d.Name)).ToList();
            int li = Math.Max(0, layoutNames.IndexOf(c.LayoutName));
            int nli = ui.Picker("lay.pick", new RectangleF(px, cy + 22, pw, 42), layoutNames.ToArray(), li, "Layout");
            if (nli != li) { c.LayoutName = layoutNames[nli]; Store.SaveSettings(); }
            cy += 78;
            if (c.LayoutName == Layouts.Auto)
            {
                Label(ui, "Columns", px, cy);
                int ci = Math.Max(0, Math.Min(6, c.LayoutCols));
                int nc = ui.Segmented("lay.cols", new RectangleF(px, cy + 22, pw, 40), new string[] { "Auto", "1", "2", "3", "4", "5", "6" }, ci);
                if (nc != c.LayoutCols) { c.LayoutCols = nc; Store.SaveSettings(); }
                cy += 78;
            }
            Label(ui, "Space between windows", px, cy);
            ui.Text(c.LayoutGap + " px", ctl.Right - 26 - 50, cy, Fonts.SmallB, Pal.A);
            int gap = ui.Slider("lay.gap", new RectangleF(px, cy + 28, pw, 24), c.LayoutGap, 0, 30);
            if (gap != c.LayoutGap) { c.LayoutGap = gap; Store.SaveSettings(); }
            cy += 68;

            bool aa = Tog(ui, "lay.auto", "Rearrange automatically", "Fits the windows again when one opens or closes.", px, cy, pw, c.AutoArrange);
            if (aa != c.AutoArrange) { c.AutoArrange = aa; Store.SaveSettings(); } cy += 54;
            bool bl = Tog(ui, "lay.bl", "Borderless", "Removes the title bar.", px, cy, pw, c.LayoutBorderless);
            if (bl != c.LayoutBorderless) { c.LayoutBorderless = bl; Store.SaveSettings(); } cy += 54;
            bool tm = Tog(ui, "lay.tm", "Always on top", "Keeps the windows above others.", px, cy, pw, c.LayoutTopMost);
            if (tm != c.LayoutTopMost) { c.LayoutTopMost = tm; Store.SaveSettings(); } cy += 54;
            bool rn = Tog(ui, "lay.rn", "Name windows", "Shows the account name as the title.", px, cy, pw, c.LayoutRename);
            if (rn != c.LayoutRename) { c.LayoutRename = rn; Store.SaveSettings(); } cy += 62;
            if (ui.Button("lay.go", new RectangleF(px, cy, pw, 48), "Arrange now", Btn.Primary, "grid")) canvas.Post(delegate { Engine.ArrangeNow(true); });
            ui.EndScroll();

            // preview of every target screen with the slots
            RectangleF pv = new RectangleF(a.X + lw + 16, y, a.Width - lw - 16, ctl.Height);
            ui.Glass(pv, 18, 0);
            List<Screen> targets = c.LayoutAllScreens ? screens.OrderBy(s => !s.Primary).ThenBy(s => s.Bounds.X).ToList() : new List<Screen> { screens[Math.Max(0, Math.Min(c.LayoutMonitor, screens.Length - 1))] };
            int n = Math.Max(Engine.Live.Count, Math.Max(selected.Count, 1));
            if (n == 1 && Engine.Live.Count == 0 && selected.Count == 0) n = 4;
            int[] counts = Engine.Spread(n, targets.Select(s => (double)s.WorkingArea.Width * s.WorkingArea.Height).ToArray());
            // lay the screens out as they sit on the desk
            int minX = targets.Min(s => s.Bounds.X), minY = targets.Min(s => s.Bounds.Y), maxX = targets.Max(s => s.Bounds.Right), maxY = targets.Max(s => s.Bounds.Bottom);
            float scale = Math.Min((pv.Width - 60) / (maxX - minX), (pv.Height - 110) / (maxY - minY));
            float ox = pv.X + (pv.Width - (maxX - minX) * scale) / 2, oy = pv.Y + 36;
            int idx = 0;
            for (int k = 0; k < targets.Count; k++)
            {
                Rectangle b = targets[k].WorkingArea;
                RectangleF mr = new RectangleF(ox + (b.X - minX) * scale, oy + (b.Y - minY) * scale, b.Width * scale, b.Height * scale);
                ui.Fill(mr, 6, Pal.Panel);
                ui.Stroke(mr, 6, Pal.Line2, 1.4f);
                int cnt = counts[k];
                if (cnt == 0) continue;
                List<RectangleF> slots = Layouts.SlotsFor(c.LayoutName, cnt, b.Width / (double)b.Height, c.LayoutCols);
                for (int i = 0; i < cnt; i++, idx++)
                {
                    RectangleF s = slots[Math.Min(i, slots.Count - 1)];
                    float g2 = c.LayoutGap * scale / 2;
                    RectangleF cell = new RectangleF(mr.X + s.X * mr.Width + g2 + 1, mr.Y + s.Y * mr.Height + g2 + 1, Math.Max(8, s.Width * mr.Width - g2 * 2 - 2), Math.Max(8, s.Height * mr.Height - g2 * 2 - 2));
                    bool live = idx < Engine.Live.Count;
                    ui.Fill(cell, 4, live ? Pal.Panel2 : Pal.Bg);
                    ui.Stroke(cell, 4, live ? Pal.Line2 : Pal.Line, 1f);
                    string label = live ? Engine.Live[idx].Acc.Label : (idx + 1).ToString();
                    ui.TextIn(Ui.Fit(ui, label, Fonts.SmallB, cell.Width - 6), cell, Fonts.SmallB, live ? Pal.Text : Pal.Faint, StringAlignment.Center);
                }
            }
            ui.TextIn(Engine.Live.Count > 0 ? Lang.F("{0} window(s) running", Engine.Live.Count) : Lang.F("Preview with {0} window(s)", n), new RectangleF(pv.X, pv.Bottom - 56, pv.Width, 30), Fonts.Small, Pal.Muted, StringAlignment.Center);
        }

        // ------------------------------------------------------------ library and editor
        void DrawLayoutLibrary(Ui ui, RectangleF a)
        {
            float y = a.Y + 90;
            float lw = Math.Min(560, a.Width * 0.5f);
            RectangleF list = new RectangleF(a.X, y, lw, a.Height - 100);
            ui.Glass(list, 18, 0);
            List<LayoutDef> all = Layouts.All();
            RectangleF view = new RectangleF(list.X, list.Y + 8, list.Width, list.Height - 16);
            float rh = 84;
            ui.BeginScroll(view, all.Count * rh + 16, Scroll("layouts"));
            float ry = view.Y + 8;
            foreach (LayoutDef d in all.ToList())
            {
                RectangleF r = new RectangleF(list.X + 14, ry, list.Width - 40, rh - 8);
                bool cur = Store.Cfg.LayoutName == d.Name;
                if (cur) ui.Stroke(r, 12, Pal.Alpha(Pal.A, 160), 1.4f);
                DrawThumb(ui, new RectangleF(r.X + 10, r.Y + 10, 96, 58), d.Slots);
                ui.Text(Ui.Fit(ui, d.Name, Fonts.BodyB, r.Width - 330), r.X + 122, r.Y + 14, Fonts.BodyB, Pal.Text);
                ui.Text(Lang.F("{0} window(s)", d.Slots.Count) + (d.Builtin ? "" : "   yours"), r.X + 122, r.Y + 40, Fonts.Small, Pal.Muted);
                float bx = r.Right - 8;
                if (!d.Builtin) { bx -= 36; if (ui.Button("lb.d" + d.Name, new RectangleF(bx, r.Y + 20, 34, 34), "", Btn.Quiet, "trash")) { Store.UserLayouts.Remove(d); Store.SaveLayouts(); if (Store.Cfg.LayoutName == d.Name) Store.Cfg.LayoutName = Layouts.Auto; Store.SaveSettings(); break; } bx -= 4; }
                bx -= 62; if (ui.Button("lb.c" + d.Name, new RectangleF(bx, r.Y + 20, 60, 34), "Copy", Btn.Quiet, null)) { edSlots = d.Copy().Slots; edName = d.Name + " copy"; edSel = -1; }
                bx -= 64; if (ui.Button("lb.u" + d.Name, new RectangleF(bx, r.Y + 20, 62, 34), cur ? "In use" : "Use", cur ? Btn.Ghost : Btn.Primary, null)) { Store.Cfg.LayoutName = d.Name; Store.SaveSettings(); }
                ry += rh;
            }
            ui.EndScroll();

            // editor
            RectangleF ed = new RectangleF(a.X + lw + 16, y, a.Width - lw - 16, a.Height - 100);
            ui.Glass(ed, 18, 0);
            ui.Text("Make your own layout", ed.X + 26, ed.Y + 20, Fonts.H2, Pal.Text);
            ui.Text("Drag on the screen to add a window. Click a window to select it, Delete removes it.", ed.X + 26, ed.Y + 50, Fonts.Small, Pal.Muted);
            float cw = ed.Width - 52, ch = cw * 9f / 16f;
            if (ch > ed.Height - 250) { ch = ed.Height - 250; cw = ch * 16f / 9f; }
            RectangleF cv = new RectangleF(ed.X + 26, ed.Y + 82, cw, ch);
            ui.Fill(cv, 8, Pal.Bg); ui.Stroke(cv, 8, Pal.Line2, 1.4f);
            for (int i = 1; i < 24; i++)
            {
                float gx = cv.X + cv.Width * i / Snap, gy = cv.Y + cv.Height * i / Snap;
                using (Pen p = new Pen(Pal.Alpha(Pal.Line, 70), 1f)) { if (i % 6 == 0) ui.G.DrawLine(p, gx, cv.Y, gx, cv.Bottom); if (i % 6 == 0) ui.G.DrawLine(p, cv.X, gy, cv.Right, gy); }
            }
            EditCanvas(ui, cv);
            for (int i = 0; i < edSlots.Count; i++)
            {
                SlotDef s = edSlots[i];
                RectangleF r = new RectangleF(cv.X + s.X * cv.Width + 1, cv.Y + s.Y * cv.Height + 1, s.W * cv.Width - 2, s.H * cv.Height - 2);
                ui.Fill(r, 4, i == edSel ? Pal.Alpha(Pal.A, 70) : Pal.Panel2);
                ui.Stroke(r, 4, i == edSel ? Pal.A : Pal.Line2, i == edSel ? 1.6f : 1f);
                ui.TextIn((i + 1).ToString(), r, Fonts.BodyB, Pal.Text, StringAlignment.Center);
            }
            if (edDrag)
            {
                RectangleF r = DragRect(cv);
                ui.Fill(r, 4, Pal.Alpha(Pal.A, 50)); ui.Stroke(r, 4, Pal.A, 1.4f);
            }
            float by = cv.Bottom + 18;
            edName = ui.TextField("ed.name", new RectangleF(ed.X + 26, by, cw - 340, 42), edName, "Name of the layout");
            if (ui.Button("ed.save", new RectangleF(ed.X + 26 + cw - 330, by, 150, 42), "Save layout", Btn.Primary, null)) SaveEditedLayout();
            if (ui.Button("ed.clear", new RectangleF(ed.X + 26 + cw - 170, by, 82, 42), "Clear", Btn.Ghost, null)) { edSlots = new List<SlotDef>(); edSel = -1; }
            if (ui.Button("ed.del", new RectangleF(ed.X + 26 + cw - 82, by, 82, 42), "Delete", Btn.Danger, null)) DeleteSelectedSlot();
            ui.Text(Lang.F("{0} window(s)", edSlots.Count) + (edSlots.Count == 0 ? "   Draw at least one." : ""), ed.X + 26, by + 54, Fonts.Small, Pal.Faint);
        }

        RectangleF DragRect(RectangleF cv)
        {
            float x0 = Math.Min(edStart.X, edNow.X), y0 = Math.Min(edStart.Y, edNow.Y), x1 = Math.Max(edStart.X, edNow.X), y1 = Math.Max(edStart.Y, edNow.Y);
            return new RectangleF(cv.X + x0 * cv.Width, cv.Y + y0 * cv.Height, (x1 - x0) * cv.Width, (y1 - y0) * cv.Height);
        }

        PointF ToCanvas(RectangleF cv, PointF m)
        {
            float x = Math.Max(0, Math.Min(1, (m.X - cv.X) / cv.Width)), y = Math.Max(0, Math.Min(1, (m.Y - cv.Y) / cv.Height));
            return new PointF((float)Math.Round(x * Snap) / Snap, (float)Math.Round(y * Snap) / Snap);
        }

        // mouse handling for the drawing area: drag makes a window, a click selects one
        void EditCanvas(Ui ui, RectangleF cv)
        {
            PointF m = ui.Mouse;
            bool inside = cv.Contains(m) && !ui.Blocked;
            if (inside) ui.WantHand = true;
            if (ui.Pressed && inside)
            {
                int hit = -1;
                for (int i = edSlots.Count - 1; i >= 0; i--)
                {
                    SlotDef s = edSlots[i];
                    if (new RectangleF(cv.X + s.X * cv.Width, cv.Y + s.Y * cv.Height, s.W * cv.Width, s.H * cv.Height).Contains(m)) { hit = i; break; }
                }
                if (hit >= 0) edSel = hit;
                else { edDrag = true; edSel = -1; edStart = edNow = ToCanvas(cv, m); }
                ui.Active = "ed.cv";
            }
            if (edDrag && ui.Down) edNow = ToCanvas(cv, m);
            if (edDrag && ui.Released)
            {
                edDrag = false;
                float x0 = Math.Min(edStart.X, edNow.X), y0 = Math.Min(edStart.Y, edNow.Y), x1 = Math.Max(edStart.X, edNow.X), y1 = Math.Max(edStart.Y, edNow.Y);
                if (x1 - x0 >= 1.5f / Snap && y1 - y0 >= 1.5f / Snap && edSlots.Count < 64) { edSlots.Add(new SlotDef { X = x0, Y = y0, W = x1 - x0, H = y1 - y0 }); edSel = edSlots.Count - 1; }
            }
            if (ui.KeyHits.Contains(Keys.Delete) && ui.Focus == null) DeleteSelectedSlot();
        }

        void DeleteSelectedSlot()
        {
            if (edSel >= 0 && edSel < edSlots.Count) { edSlots.RemoveAt(edSel); edSel = -1; }
            else if (edSlots.Count > 0) edSlots.RemoveAt(edSlots.Count - 1);
        }

        void SaveEditedLayout()
        {
            string name = edName.Trim();
            if (name.Length == 0) { Log.Add("Give the layout a name."); return; }
            if (edSlots.Count == 0) { Log.Add("Draw at least one window first."); return; }
            if (Layouts.Builtin.Any(d => d.Name == name)) name += " (mine)";
            Store.UserLayouts.RemoveAll(d => d.Name == name);
            LayoutDef def = new LayoutDef { Name = name };
            foreach (SlotDef s in edSlots) def.Slots.Add(new SlotDef { X = s.X, Y = s.Y, W = s.W, H = s.H });
            Store.UserLayouts.Add(def);
            Store.SaveLayouts();
            Store.Cfg.LayoutName = name; Store.SaveSettings();
            Log.Add("Layout saved: " + name + ".");
        }

        void DrawThumb(Ui ui, RectangleF r, List<SlotDef> slots)
        {
            ui.Fill(r, 4, Pal.Bg); ui.Stroke(r, 4, Pal.Line2, 1f);
            foreach (SlotDef s in slots)
            {
                RectangleF c = new RectangleF(r.X + s.X * r.Width + 1, r.Y + s.Y * r.Height + 1, Math.Max(2, s.W * r.Width - 2), Math.Max(2, s.H * r.Height - 2));
                ui.Fill(c, 1.5f, Pal.Panel2); ui.Stroke(c, 1.5f, Pal.Line2, 1f);
            }
        }
    }
}
