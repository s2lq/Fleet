using System;
using System.Drawing;

namespace Fleet
{
    // the logo comes in on its own, then glides up into the top bar while the screen fades in
    public partial class App
    {
        float introStart = -1f;
        bool introDone;

        bool Intro { get { return !introDone; } }

        void DrawIntro(Ui ui)
        {
            if (introDone) return;
            if (introStart < 0f)
            {
                introStart = ui.Time;
                if (Motion.Minimal) { introDone = true; introLogoHidden = false; return; }
            }
            float t = ui.Time - introStart;
            const float hold = 0.75f, total = 1.55f;
            if (t >= total) { introDone = true; introLogoHidden = false; return; }
            ui.NeedFrames = true;
            introLogoHidden = true;

            float move = Ui.EaseInOut((t - hold) / (total - hold));
            int a = t < hold ? 250 : (int)(250 * (1f - Ui.EaseOut((t - hold) / (total - hold) * 1.15f)));
            ui.Fill(new RectangleF(0, 0, ui.W, ui.H), 0, Color.FromArgb(a, Pal.Bg.R, Pal.Bg.G, Pal.Bg.B));

            float lx = ui.W < 1150 ? 20 : 32;
            float size = 104f + (40f - 104f) * move;
            float grow = t < hold ? 0.55f + 0.45f * Ui.EaseBack(t / 0.55f) : 1f;
            float cx = ui.W / 2f + (lx + 20f - ui.W / 2f) * move, cy = ui.H / 2f - 24f + (38f - (ui.H / 2f - 24f)) * move;
            float s = size * grow;
            RectangleF logo = new RectangleF(cx - s / 2, cy - s / 2, s, s);
            ui.Logo(logo);
            float fadeIn = Math.Min(1f, t / 0.35f);
            if (fadeIn < 1f) ui.Fill(new RectangleF(logo.X - 2, logo.Y - 2, logo.Width + 4, logo.Height + 4), logo.Width * 0.22f, Color.FromArgb((int)(255 * (1f - fadeIn)), Pal.Bg.R, Pal.Bg.G, Pal.Bg.B));

            // the name under the logo, gone by the time it moves
            float nameA = Math.Min(1f, Math.Max(0f, (t - 0.25f) / 0.3f)) * (1f - Math.Min(1f, move * 2.2f));
            if (nameA > 0.01f)
            {
                Font f = Fonts.Get(26, true);
                SizeF m = ui.Measure("Fleet", f);
                ui.Text("Fleet", ui.W / 2f - m.Width / 2f, cy + 68f + (1f - nameA) * 8f, f, Pal.Mix(Pal.Bg, Pal.Text, nameA));
            }
            // a thin bar that fills while it gets ready
            float bw = 120f;
            float p = Math.Min(1f, t / hold);
            if (move < 0.01f)
            {
                ui.Fill(new RectangleF(ui.W / 2f - bw / 2, cy + 118f, bw, 3), 1.5f, Pal.Line);
                ui.Fill(new RectangleF(ui.W / 2f - bw / 2, cy + 118f, Math.Max(3f, bw * Ui.EaseInOut(p)), 3), 1.5f, Pal.Text);
            }
        }
    }
}
