using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Fleet
{
    public partial class App
    {
        int wizStep;
        int wizConfig = -1;
        bool wizPortable;

        const int WizSteps = 5;
        int wizDir = 1;

        public void WizAt(int i) { wizStep = i; }

        void WizGo(int to) { wizDir = to > wizStep ? 1 : -1; wizStep = Math.Max(0, Math.Min(WizSteps - 1, to)); }

        // first start: welcome and language, look, multi-instance, performance and data, first account
        void DrawWizard(Ui ui, RectangleF area)
        {
            float w = Math.Min(700, ui.W - 40), h = 520;
            float enter = ui.Appear("wiz" + wizStep, 0);
            float rise = ui.Appear("wizcard", 0);
            RectangleF card = new RectangleF((ui.W - w) / 2, Math.Max(90, (ui.H - h) / 2 + 20) + (1f - rise) * 30f, w, h);
            ui.Fill(new RectangleF(0, 76, ui.W, ui.H - 76), 0, Color.FromArgb((int)(120 * rise), Pal.Bg.R, Pal.Bg.G, Pal.Bg.B));
            ui.Glass(card, 18, 0);

            // progress: one segment per step that fills in
            float x = card.X + 40, cw = card.Width - 80, y = card.Y + 30;
            float seg = (cw - (WizSteps - 1) * 6) / WizSteps;
            for (int i = 0; i < WizSteps; i++)
            {
                float fill = ui.Anim("wzp" + i, i <= wizStep ? 1f : 0f, 0.2f);
                RectangleF sr = new RectangleF(x + i * (seg + 6), y, seg, 4);
                ui.Fill(sr, 2, Pal.Line);
                if (fill > 0.01f) ui.Fill(new RectangleF(sr.X, sr.Y, Math.Max(4, sr.Width * fill), 4), 2, Pal.A);
            }
            y += 30;

            // content slides in from the side it came from
            GraphicsState gs = ui.G.Save();
            ui.G.TranslateTransform((1f - enter) * 36f * wizDir, 0);
            ui.G.SetClip(new RectangleF(card.X + 2, y - 4, card.Width - 4, card.Height - 90));
            switch (wizStep)
            {
                case 0:
                    {
                        float s = 0.6f + 0.4f * Ui.EaseBack(ui.Appear("wzlogo", 0));
                        float ls = 76 * s;
                        ui.Logo(new RectangleF(x + 38 - ls / 2, y + 38 - ls / 2 + 4, ls, ls));
                        ui.Text("Welcome to Fleet", x, y + 96, Fonts.H1, Pal.Text);
                        ui.Para("Run all your Roblox accounts at once, all loading at the same moment and lining up by themselves. Setting up takes about a minute.", new RectangleF(x, y + 146, cw, 70), Fonts.Body, Pal.Muted);
                        Label(ui, "Language", x, y + 236);
                        List<string[]> langs = Lang.Available();
                        int li = Math.Max(0, langs.FindIndex(l => l[0] == Store.Cfg.Language));
                        int nl = ui.Picker("wz.lang", new RectangleF(x, y + 260, cw, 44), langs.Select(l => l[1]).ToArray(), li, "Language");
                        if (nl != li) { Store.Cfg.Language = langs[nl][0]; Lang.Load(Store.Cfg.Language); }
                        break;
                    }
                case 1:
                    {
                        ui.Text("Pick a look", x, y, Fonts.H1, Pal.Text);
                        ui.Text("It changes right away. You can change it any time in Settings.", x, y + 44, Fonts.Body, Pal.Muted);
                        int cols = 6; float gap = 10, tw = (cw - (cols - 1) * gap) / cols, th = 62;
                        for (int i = 0; i < Themes.Presets.Count; i++)
                        {
                            Themes.Def d = Themes.Presets[i];
                            RectangleF r = new RectangleF(x + (i % cols) * (tw + gap), y + 84 + (i / cols) * (th + 34), tw, th);
                            bool over = ui.Over(r);
                            if (over) ui.WantHand = true;
                            float hot = ui.Anim("wzt" + i, over ? 1f : 0f, 0.3f);
                            bool sel = Store.Cfg.Theme == d.Name;
                            RectangleF rr = new RectangleF(r.X, r.Y - hot * 2, r.Width, r.Height);
                            ui.Fill(rr, 10, d.Bg);
                            ui.Fill(new RectangleF(rr.X + 8, rr.Y + 10, rr.Width * 0.58f, rr.Height - 20), 5, d.Panel);
                            ui.Fill(new RectangleF(rr.X + 14, rr.Y + 18, rr.Width * 0.3f, 4), 2, d.Text);
                            ui.Fill(new RectangleF(rr.X + 14, rr.Y + 28, rr.Width * 0.42f, 3), 1.5f, d.Muted);
                            ui.Fill(new RectangleF(rr.Right - 17, rr.Y + 10, 9, 9), 4.5f, d.Text);
                            ui.Stroke(rr, 10, sel ? Pal.Text : Pal.Mix(d.Line, Pal.Text, hot * 0.5f), sel ? 2f : 1f);
                            ui.Text(Ui.Fit(ui, d.Name, Fonts.Small, tw + 6), r.X + 2, r.Bottom + 7, Fonts.Small, sel ? Pal.Text : Pal.Muted);
                            if (ui.Pressed && over) ui.Active = "wzt" + i;
                            if (ui.Released && ui.Active == "wzt" + i) { ui.Active = null; if (over) { Store.Cfg.Theme = d.Name; Themes.Apply(d.Name); Store.SaveSettings(); } }
                        }
                        float ay = y + 84 + 2 * (th + 34) + 6;
                        Label(ui, "Accent", x, ay);
                        for (int i = 0; i < Pal.Accents.Length; i++)
                        {
                            RectangleF sw = new RectangleF(x + i * 44, ay + 26, 28, 28);
                            bool over = ui.Over(sw);
                            if (over) ui.WantHand = true;
                            float grow = ui.Anim("wzs" + i, over ? 1f : 0f, 0.3f);
                            ui.Fill(new RectangleF(sw.X - grow * 2, sw.Y - grow * 2, sw.Width + grow * 4, sw.Height + grow * 4), 16, i == 0 ? Pal.Text : Pal.Accents[i].A);
                            if (Store.Cfg.Accent == i) using (Pen pen = new Pen(Pal.Text, 2f)) ui.G.DrawEllipse(pen, sw.X - 4, sw.Y - 4, sw.Width + 8, sw.Height + 8);
                            if (ui.Pressed && over) ui.Active = "wzs" + i;
                            if (ui.Released && ui.Active == "wzs" + i) { ui.Active = null; if (over) { Store.Cfg.Accent = i; Store.SaveSettings(); } }
                        }
                        break;
                    }
                case 2:
                    {
                        ui.Text("Multi-instance", x, y, Fonts.H1, Pal.Text);
                        ui.Para("Fleet holds Roblox's single-instance lock so several windows can run. Nothing is injected into Roblox. Roblox may treat several clients as against its rules and can change the client at any time: you use this at your own risk, with your own accounts.", new RectangleF(x, y + 50, cw, 120), Fonts.Body, Pal.Muted);
                        bool mi = Tog(ui, "wz.mi", "Turn multi-instance on", "You can change this later in Settings.", x, y + 190, cw, Store.Cfg.MultiInstance);
                        if (mi != Store.Cfg.MultiInstance) { Store.Cfg.MultiInstance = mi; }
                        break;
                    }
                case 3:
                    {
                        ui.Text("How heavy may Roblox be?", x, y, Fonts.H1, Pal.Text);
                        ui.Para("Pick a starting point for graphics, frame rate and limits. You can change it later on the Performance page.", new RectangleF(x, y + 50, cw, 60), Fonts.Body, Pal.Muted);
                        string[] opts = new string[] { "Keep the defaults" }.Concat(Configs.Quick.Select(q => q.Name + "  -  " + q.Tagline)).ToArray();
                        int ci = ui.Picker("wz.cfg", new RectangleF(x, y + 120, cw, 44), opts, wizConfig + 1, "Choose");
                        wizConfig = ci - 1;
                        ui.Text("Your data", x, y + 196, Fonts.BodyB, Pal.Text);
                        int dm = ui.Segmented("wz.data", new RectangleF(x, y + 224, cw, 42), new string[] { "Normal (AppData)", "Portable (next to Fleet.exe)" }, wizPortable ? 1 : 0);
                        wizPortable = dm == 1;
                        break;
                    }
                default:
                    {
                        ui.Text("Add your accounts", x, y, Fonts.H1, Pal.Text);
                        ui.Para("Log in with a browser (Fleet reads the login for you), or paste cookies. You can also do this later on the Accounts page. A quick tour of the app comes next.", new RectangleF(x, y + 50, cw, 90), Fonts.Body, Pal.Muted);
                        if (ui.Button("wz.br", new RectangleF(x, y + 150, cw, 50), "Log in with a browser", Btn.Primary, "plus")) { FinishWizard(); BrowserAdd(); Go("Accounts"); }
                        if (ui.Button("wz.pa", new RectangleF(x, y + 210, cw, 50), "Paste cookies", Btn.Ghost, null)) { FinishWizard(); Go("Accounts"); AddAccounts(); }
                        break;
                    }
            }
            ui.G.ResetClip();
            ui.G.Restore(gs);
            if (enter < 0.999f) ui.Fill(new RectangleF(card.X + 2, y - 4, card.Width - 4, card.Height - 90), 0, Color.FromArgb((int)((1f - enter) * 255), Pal.Panel.R, Pal.Panel.G, Pal.Panel.B));

            float by = card.Bottom - 72;
            if (wizStep > 0) { if (ui.Button("wz.back", new RectangleF(x, by, 110, 44), "Back", Btn.Ghost, null)) WizGo(wizStep - 1); }
            else if (ui.Button("wz.skip", new RectangleF(x, by, 140, 44), "Skip setup", Btn.Quiet, null)) FinishWizard();
            string next = wizStep == WizSteps - 1 ? "Skip for now" : (wizStep == 0 ? "Get started" : "Next");
            if (ui.Button("wz.next", new RectangleF(card.Right - 40 - 160, by, 160, 44), next, wizStep == WizSteps - 1 ? Btn.Ghost : Btn.Primary, null))
            {
                if (wizStep == WizSteps - 1) FinishWizard(); else WizGo(wizStep + 1);
            }
        }

        void FinishWizard()
        {
            Store.Cfg.FirstRunDone = true;
            tourNotBefore = Environment.TickCount + 900;
            if (wizConfig >= 0) { Configs.Quick[wizConfig].Apply(Store.Cfg); Perf.CapChanged(); }
            Store.SaveSettings();
            Engine.SetMultiInstance(Store.Cfg.MultiInstance);
            if (wizPortable && !Store.IsPortable)
                canvas.Post(delegate
                {
                    if (!EnsurePortablePassword()) return;
                    string err = Store.SetPortable(true);
                    if (err != null) Log.Add("Could not switch to portable mode: " + err); else Application.Restart();
                });
        }
    }
}
