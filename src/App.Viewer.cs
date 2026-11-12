using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;

namespace Fleet
{
    // click an account's picture to look at its 3D avatar
    public partial class App
    {
        Account viewing;
        AvatarView view3d;
        string viewStatus = "";
        bool viewFailed;
        PointF lastDrag;
        readonly Dictionary<long, Mesh> meshCache = new Dictionary<long, Mesh>();

        public void OpenViewer(Account a)
        {
            CloseViewer();
            viewing = a;
            viewFailed = false;
            view3d = new AvatarView();
            Mesh cached;
            if (meshCache.TryGetValue(a.UserId, out cached)) { view3d.Model = cached; viewStatus = ""; }
            else LoadViewer(a, view3d);
        }

        async void LoadViewer(Account a, AvatarView v)
        {
            try
            {
                Mesh m = await Avatar3D.Load(a, delegate(string s) { viewStatus = s; });
                if (m.Triangles == 0) throw new InvalidOperationException("The model is empty.");
                meshCache[a.UserId] = m;
                if (view3d == v) { v.Model = m; viewStatus = ""; }
            }
            catch (Exception ex) { if (view3d == v) { viewFailed = true; viewStatus = ex.Message; } }
        }

        void CloseViewer()
        {
            if (view3d != null) view3d.Dispose();
            view3d = null; viewing = null; viewStatus = "";
        }

        // a clickable picture: shows a hand and a ring on hover, opens the viewer on click
        void AvatarButton(Ui ui, RectangleF r, Account a)
        {
            ui.Avatar(r, a);
            bool over = ui.Over(r);
            float h = ui.Anim("av." + a.Id, over);
            if (over) ui.WantHand = true;
            if (h > 0.01f) using (Pen p = new Pen(Pal.Alpha(Pal.A, (int)(200 * h)), 2f)) ui.G.DrawEllipse(p, r.X - 2, r.Y - 2, r.Width + 4, r.Height + 4);
            if (ui.Pressed && over) ui.Active = "av" + a.Id;
            if (ui.Released && ui.Active == "av" + a.Id) { ui.Active = null; if (over) OpenViewer(a); }
        }

        void DrawViewer(Ui ui)
        {
            Account a = viewing;
            float w = Math.Min(560, ui.W - 40), h = Math.Min(700, ui.H - 30);
            float e = ui.Appear("viewer", 0);
            RectangleF card = new RectangleF((ui.W - w) / 2, Math.Max(15, (ui.H - h) / 2) + (1f - e) * 24f, w, h);
            ui.Fill(new RectangleF(0, 0, ui.W, ui.H), 0, Color.FromArgb((int)(190 * e), Pal.Bg.R, Pal.Bg.G, Pal.Bg.B));
            if (ui.Pressed && !card.Contains(ui.Mouse)) { ui.Forget("viewer"); CloseViewer(); return; }
            ui.Fill(new RectangleF(card.X - 1, card.Y - 1, card.Width + 2, card.Height + 2), 14, Pal.Line);
            ui.Fill(card, 13, Pal.Panel);

            ui.Text(Ui.Fit(ui, a.Label, Fonts.H2, w - 80), card.X + 28, card.Y + 22, Fonts.H2, Pal.Text);
            ui.Text("@" + a.Name, card.X + 28, card.Y + 50, Fonts.Small, Pal.Muted);
            if (ui.Button("vw.x", new RectangleF(card.Right - 28 - 36, card.Y + 20, 36, 36), "", Btn.Quiet, "x")) { ui.Forget("viewer"); CloseViewer(); return; }

            float ah = card.Height - 190, aw = ah * view3d.W / view3d.H;
            if (aw > w - 56) { aw = w - 56; ah = aw * view3d.H / view3d.W; }
            RectangleF area = new RectangleF(card.X + (w - aw) / 2, card.Y + 84, aw, ah);
            ui.Fill(area, 12, Pal.Bg);
            ui.Stroke(area, 12, Pal.Line, 1f);

            Bitmap frame = view3d.Frame;
            if (frame != null)
            {
                GraphicsState gs = ui.G.Save();
                using (GraphicsPath clip = Ui.Round(area, 12)) ui.G.SetClip(clip, CombineMode.Intersect);
                ui.G.InterpolationMode = InterpolationMode.HighQualityBilinear;
                try { ui.G.DrawImage(frame, area); } catch (Exception) { }
                ui.G.Restore(gs);
            }
            else if (viewFailed) ui.Para(viewStatus, new RectangleF(area.X + 24, area.Y + area.Height / 2 - 40, area.Width - 48, 100), Fonts.Body, Pal.Muted);
            else
            {
                ui.Spinner(new RectangleF(area.X + area.Width / 2 - 12, area.Y + area.Height / 2 - 30, 24, 24), Pal.A);
                ui.TextIn(viewStatus, new RectangleF(area.X + 20, area.Y + area.Height / 2 + 4, area.Width - 40, 30), Fonts.Body, Pal.Muted, StringAlignment.Center);
            }

            // drag to turn, wheel to zoom
            bool inArea = area.Contains(ui.Mouse);
            if (inArea) ui.WantHand = true;
            if (ui.Pressed && inArea) { ui.Active = "vw.drag"; lastDrag = ui.Mouse; view3d.Auto = false; }
            if (ui.Active == "vw.drag" && ui.Down)
            {
                view3d.Yaw += (ui.Mouse.X - lastDrag.X) * 0.012f;
                view3d.Pitch = Math.Max(-0.9f, Math.Min(0.9f, view3d.Pitch + (ui.Mouse.Y - lastDrag.Y) * 0.008f));
                lastDrag = ui.Mouse;
            }
            if (inArea && Math.Abs(ui.Wheel) > 0.1f) view3d.Zoom = Math.Max(0.5f, Math.Min(3f, view3d.Zoom * (1f + ui.Wheel / 1500f)));
            ui.NeedFrames = true;

            float by = card.Bottom - 74;
            ui.Text("Drag to turn, scroll to zoom.", card.X + 28, by - 26, Fonts.Small, Pal.Faint);
            float bx = card.X + 28;
            if (ui.Button("vw.auto", new RectangleF(bx, by, 130, 42), view3d.Auto ? "Spinning" : "Spin", view3d.Auto ? Btn.Primary : Btn.Ghost, null)) view3d.Auto = !view3d.Auto; bx += 138;
            if (ui.Button("vw.reset", new RectangleF(bx, by, 110, 42), "Reset", Btn.Ghost, null)) { view3d.Yaw = 0; view3d.Pitch = 0; view3d.Zoom = 1; view3d.Auto = true; } bx += 118;
            if (ui.Button("vw.ref", new RectangleF(bx, by, 120, 42), "Reload", Btn.Ghost, "refresh")) { meshCache.Remove(a.UserId); OpenViewer(a); return; }
            if (ui.Button("vw.det", new RectangleF(card.Right - 28 - 120, by, 120, 42), "Details", Btn.Ghost, "info")) { Account q = a; ui.Forget("viewer"); CloseViewer(); OpenDetails(q); }
        }
    }
}
