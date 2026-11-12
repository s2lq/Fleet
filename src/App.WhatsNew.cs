using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace Fleet
{
    // a window that lists what changed, shown once after an update
    public partial class App
    {
        List<ChangeEntry> news;
        readonly ScrollState newsScroll = new ScrollState();

        public void CheckWhatsNew()
        {
            string last = Store.Cfg.LastSeenVersion;
            Store.Cfg.LastSeenVersion = Program.Version;
            Store.SaveSettings();
            if (string.IsNullOrEmpty(last) || last == Program.Version) return;
            List<ChangeEntry> list = Changes.Since(last);
            if (list.Count > 0) news = list;
        }

        void ShowChanges() { news = Changes.All().Take(3).ToList(); newsScroll.Pos = newsScroll.Target = 0; }

        void DrawWhatsNew(Ui ui)
        {
            float w = Math.Min(660, ui.W - 40), h = Math.Min(640, ui.H - 40);
            float e = ui.Appear("news", 0);
            RectangleF card = new RectangleF((ui.W - w) / 2, (ui.H - h) / 2 + (1f - e) * 26f, w, h);
            ui.Fill(new RectangleF(0, 0, ui.W, ui.H), 0, Color.FromArgb((int)(190 * e), Pal.Bg.R, Pal.Bg.G, Pal.Bg.B));
            if (ui.Pressed && !card.Contains(ui.Mouse)) { CloseNews(ui); return; }
            ui.Fill(new RectangleF(card.X - 1, card.Y - 1, card.Width + 2, card.Height + 2), 14, Pal.Line);
            ui.Fill(card, 13, Pal.Panel);

            ui.Logo(new RectangleF(card.X + 32, card.Y + 28, 44, 44));
            ui.Text("What's new in Fleet", card.X + 92, card.Y + 26, Fonts.H2, Pal.Text);
            ui.Text(Lang.F("You are on version {0}", Program.Version), card.X + 92, card.Y + 54, Fonts.Small, Pal.Muted);

            RectangleF view = new RectangleF(card.X + 8, card.Y + 92, card.Width - 16, card.Height - 92 - 84);
            float content = 20;
            foreach (ChangeEntry en in news) content += 52 + en.Items.Sum(i => LineHeight(ui, i, view.Width - 90)) + 14;
            ui.BeginScroll(view, content, newsScroll);
            float y = view.Y + 10;
            int n = 0;
            foreach (ChangeEntry en in news)
            {
                float a = ui.Appear("nw" + en.Version, n++);
                GraphicsState gs = ui.G.Save();
                ui.G.TranslateTransform(0, (1f - a) * 18f);
                ui.Chip("v" + en.Version, card.X + 32, y, Pal.A);
                y += 40;
                int k = 0;
                foreach (string item in en.Items)
                {
                    float ia = ui.Appear("nwi" + en.Version + k++, n + k);
                    float lh = LineHeight(ui, item, view.Width - 90);
                    ui.Fill(new RectangleF(card.X + 38, y + 7, 6, 6), 3, Pal.Alpha(Pal.A, (int)(255 * ia)));
                    ui.Para(item, new RectangleF(card.X + 58, y, view.Width - 90, lh), Fonts.Body, Pal.Mix(Pal.Muted, Pal.Text, ia));
                    y += lh;
                }
                ui.G.Restore(gs);
                y += 14;
            }
            ui.EndScroll();

            float by = card.Bottom - 66;
            if (ui.Button("nw.ok", new RectangleF(card.Right - 32 - 130, by, 130, 42), "Got it", Btn.Primary, null)) { CloseNews(ui); return; }
            if (ui.Button("nw.all", new RectangleF(card.Right - 32 - 130 - 12 - 190, by, 190, 42), "Full changelog", Btn.Ghost, null))
            {
                try { Process.Start(new ProcessStartInfo("https://github.com/" + Updater.Repo + "/blob/main/CHANGELOG.md") { UseShellExecute = true }); } catch (Exception) { }
            }
        }

        float LineHeight(Ui ui, string text, float width)
        {
            StringFormat sf = new StringFormat(StringFormat.GenericTypographic);
            return (float)Math.Ceiling(ui.G.MeasureString(Lang.T(text), Fonts.Body, (int)width, sf).Height) + 8;
        }

        void CloseNews(Ui ui) { news = null; ui.Forget("news"); }
    }
}
