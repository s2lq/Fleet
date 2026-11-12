using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Fleet
{
    // a short guided tour for new users: it walks through the pages, lights up each tab and explains it. It can be skipped at any time.
    public partial class App
    {
        class TourStep { public string Page, Title, Text; public bool Center; }

        static readonly TourStep[] Steps =
        {
            new TourStep { Page = "Overview", Center = true, Title = "Welcome to Fleet", Text = "Take a one minute tour of the pages, or skip it and explore on your own. You can replay it any time from Settings > About." },
            new TourStep { Page = "Accounts", Title = "Your accounts", Text = "Add accounts with Browser login or by pasting cookies. Cards show who is signed in and who is running. Click a picture to see its 3D avatar, the i button for age, Premium and friends." },
            new TourStep { Page = "Launch", Title = "Launch", Text = "Pick a game by id, link or name, choose who goes, and press Launch. With Start all at once on, every window opens at the same moment." },
            new TourStep { Page = "Servers", Title = "Servers", Text = "Save private servers and hand them to accounts, or browse a game's public servers and pick one by players or ping." },
            new TourStep { Page = "Layout", Title = "Layout", Text = "The Roblox windows fill the screen edge to edge and rearrange themselves when one opens or closes. Use All screens to spread over every monitor." },
            new TourStep { Page = "Health", Title = "Health", Text = "See which logins work, which are signed out or locked, and recover them. Crashes and kicks are logged per account." },
            new TourStep { Page = "Performance", Title = "Performance", Text = "Cap the frame rate, CPU and memory of each window, slow the ones you are not using, and clean the memory of the whole PC." },
            new TourStep { Page = "Tools", Title = "Tools", Text = "Presets, a scheduler, a macro recorder, a gallery of shared configs and themes, and handy account tools like bulk edit." },
            new TourStep { Page = "Settings", Title = "Settings", Text = "Dark and light themes, ten languages, portable mode, proxies, Discord alerts, the Roblox version manager and more." },
            new TourStep { Page = "Overview", Center = true, Title = "You are ready", Text = "Ctrl+F searches accounts, Ctrl+L launches the selected ones, Esc closes windows. Add your first account and go." }
        };

        int tourStep = -1;
        readonly Dictionary<string, RectangleF> navRects = new Dictionary<string, RectangleF>();

        public void StartTour() { StartTourAt(0); }
        public void StartTourAt(int i) { tourStep = i; page = Steps[i].Page; }

        void EndTour(Ui ui)
        {
            tourStep = -1;
            Store.Cfg.TourDone = true;
            Store.SaveSettings();
            ui.Forget("tour");
        }

        public bool TourActive { get { return tourStep >= 0; } }

        public bool TourKey(Keys k)
        {
            if (tourStep < 0) return false;
            if (k == Keys.Right || k == Keys.Enter || k == Keys.Space) { TourNext(); return true; }
            if (k == Keys.Left) { if (tourStep > 0) { tourStep--; page = Steps[tourStep].Page; } return true; }
            if (k == Keys.Escape) { tourStep = -1; Store.Cfg.TourDone = true; Store.SaveSettings(); return true; }
            return false;
        }

        void TourNext()
        {
            if (tourStep >= Steps.Length - 1) { tourStep = -1; Store.Cfg.TourDone = true; Store.SaveSettings(); return; }
            tourStep++;
            page = Steps[tourStep].Page;
        }

        void DrawTour(Ui ui)
        {
            TourStep st = Steps[tourStep];
            float t = ui.Appear("tour" + tourStep, 0);

            // spotlight on the tab being explained
            RectangleF hole = RectangleF.Empty;
            RectangleF target;
            if (!st.Center && navRects.TryGetValue(st.Page, out target))
                hole = new RectangleF(ui.Anim("th.x", target.X - 6, 0.22f), ui.Anim("th.y", target.Y - 6, 0.22f), ui.Anim("th.w", target.Width + 12, 0.22f), ui.Anim("th.h", target.Height + 12, 0.22f));
            using (GraphicsPath p = new GraphicsPath(FillMode.Alternate))
            {
                p.AddRectangle(new RectangleF(0, 0, ui.W, ui.H));
                if (!hole.IsEmpty) p.AddPath(Ui.Round(hole, hole.Height / 2), false);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(200, Pal.Bg.R, Pal.Bg.G, Pal.Bg.B))) ui.G.FillPath(b, p);
            }
            if (!hole.IsEmpty)
            {
                using (Pen pen = new Pen(Pal.Alpha(Pal.A, 220), 2f)) using (GraphicsPath hp = Ui.Round(hole, hole.Height / 2)) ui.G.DrawPath(pen, hp);
                ui.Pulse(hole.X + hole.Width / 2, hole.Y + hole.Height / 2, Math.Max(hole.Width, hole.Height) / 2.6f, Pal.A);
            }

            // the card
            float w = Math.Min(460, ui.W - 40), h = 252;
            float cx = st.Center || hole.IsEmpty ? (ui.W - w) / 2 : Math.Max(20, Math.Min(ui.W - w - 20, hole.X + hole.Width / 2 - w / 2));
            float cy = st.Center || hole.IsEmpty ? (ui.H - h) / 2 : hole.Bottom + 22;
            RectangleF card = new RectangleF(cx, cy + (1f - t) * 22f, w, h);
            if (!hole.IsEmpty)
            {
                float ax = Math.Max(card.X + 24, Math.Min(card.Right - 24, hole.X + hole.Width / 2));
                using (SolidBrush b = new SolidBrush(Pal.Line2)) ui.G.FillPolygon(b, new PointF[] { new PointF(ax - 10, card.Y + 0.5f), new PointF(ax + 10, card.Y + 0.5f), new PointF(ax, card.Y - 11) });
                using (SolidBrush b = new SolidBrush(Pal.Panel)) ui.G.FillPolygon(b, new PointF[] { new PointF(ax - 9, card.Y + 1.5f), new PointF(ax + 9, card.Y + 1.5f), new PointF(ax, card.Y - 9) });
            }
            ui.Fill(new RectangleF(card.X - 1, card.Y - 1, card.Width + 2, card.Height + 2), 14, Pal.Line2);
            ui.Frost(card, 13, Pal.Panel, 205);

            // progress bar along the top of the card
            float prog = ui.Anim("tour.p", (tourStep + 1f) / Steps.Length, 0.18f);
            ui.Fill(new RectangleF(card.X + 20, card.Y + 14, card.Width - 40, 4), 2, Pal.Line);
            ui.Fill(new RectangleF(card.X + 20, card.Y + 14, Math.Max(6, (card.Width - 40) * prog), 4), 2, Pal.A);

            ui.Text(Steps.Length > 2 ? Lang.F("Step {0} of {1}", tourStep + 1, Steps.Length) : "", card.X + 24, card.Y + 30, Fonts.SmallB, Pal.Muted);
            ui.Text(st.Title, card.X + 24, card.Y + 50, Fonts.H2, Pal.Text);
            ui.Para(st.Text, new RectangleF(card.X + 24, card.Y + 86, card.Width - 48, 80), Fonts.Body, Pal.Muted);

            float by = card.Bottom - 56;
            if (ui.Button("tour.skip", new RectangleF(card.X + 20, by, 110, 40), "Skip tour", Btn.Quiet, null)) { EndTour(ui); return; }
            bool last = tourStep == Steps.Length - 1;
            if (ui.Button("tour.next", new RectangleF(card.Right - 20 - 120, by, 120, 40), tourStep == 0 ? "Start" : (last ? "Finish" : "Next"), Btn.Primary, null))
            {
                if (last) { EndTour(ui); return; }
                TourNext();
            }
            if (tourStep > 0 && ui.Button("tour.back", new RectangleF(card.Right - 20 - 120 - 8 - 90, by, 90, 40), "Back", Btn.Ghost, null)) { tourStep--; page = Steps[tourStep].Page; }
            // step dots
            float dx = card.X + card.Width / 2 - Steps.Length * 7;
            for (int i = 0; i < Steps.Length; i++)
            {
                float dw = ui.Anim("tdot" + i, i == tourStep ? 1f : 0f, 0.25f);
                ui.Fill(new RectangleF(dx + i * 14, by - 22, 6 + 6 * dw, 6), 3, i == tourStep ? Pal.A : Pal.Line2);
                dx += 6 * dw;
            }
        }
    }
}
