using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fleet
{
    public partial class App
    {
        int wizStep;
        int wizConfig = -1;
        bool wizPortable;

        // first start: language, multi-instance, a quick config, where the data lives, first account
        void DrawWizard(Ui ui, RectangleF area)
        {
            float w = Math.Min(640, ui.W - 40), h = 470;
            float enter = ui.Appear("wiz" + wizStep, 0);
            RectangleF card = new RectangleF((ui.W - w) / 2, Math.Max(90, (ui.H - h) / 2) + (1f - enter) * 24f, w, h);
            ui.Fill(new RectangleF(0, 76, ui.W, ui.H - 76), 0, Color.FromArgb(150, 0, 0, 0));
            ui.Glass(card, 18, 0);
            float x = card.X + 36, cw = card.Width - 72, y = card.Y + 32;
            ui.Text(Lang.F("Step {0} of 4", wizStep + 1), x, y, Fonts.SmallB, Pal.Muted);
            y += 28;
            switch (wizStep)
            {
                case 0:
                    {
                        ui.Text("Welcome to Fleet", x, y, Fonts.H1, Pal.Text);
                        ui.Para("Run all your Roblox accounts at once. Pick your language to start.", new RectangleF(x, y + 50, cw, 60), Fonts.Body, Pal.Muted);
                        List<string[]> langs = Lang.Available();
                        int li = Math.Max(0, langs.FindIndex(l => l[0] == Store.Cfg.Language));
                        int nl = ui.Picker("wz.lang", new RectangleF(x, y + 120, cw, 44), langs.Select(l => l[1]).ToArray(), li, "Language");
                        if (nl != li) { Store.Cfg.Language = langs[nl][0]; Lang.Load(Store.Cfg.Language); }
                        break;
                    }
                case 1:
                    {
                        ui.Text("Multi-instance", x, y, Fonts.H1, Pal.Text);
                        ui.Para("Fleet holds Roblox's single-instance lock so several windows can run. Nothing is injected into Roblox. Roblox may treat several clients as against its rules and can change the client at any time: you use this at your own risk, with your own accounts.", new RectangleF(x, y + 50, cw, 120), Fonts.Body, Pal.Muted);
                        bool mi = Tog(ui, "wz.mi", "Turn multi-instance on", "You can change this later in Settings.", x, y + 180, cw, Store.Cfg.MultiInstance);
                        if (mi != Store.Cfg.MultiInstance) { Store.Cfg.MultiInstance = mi; }
                        break;
                    }
                case 2:
                    {
                        ui.Text("How heavy may Roblox be?", x, y, Fonts.H1, Pal.Text);
                        ui.Para("Pick a starting point for graphics, frame rate and limits. You can change it later on the Performance page.", new RectangleF(x, y + 50, cw, 60), Fonts.Body, Pal.Muted);
                        string[] opts = new string[] { "Keep the defaults" }.Concat(Configs.Quick.Select(q => q.Name + "  -  " + q.Tagline)).ToArray();
                        int ci = ui.Picker("wz.cfg", new RectangleF(x, y + 120, cw, 44), opts, wizConfig + 1, "Choose");
                        wizConfig = ci - 1;
                        string note = "Your data";
                        ui.Text(note, x, y + 190, Fonts.BodyB, Pal.Text);
                        int dm = ui.Segmented("wz.data", new RectangleF(x, y + 218, cw, 42), new string[] { "Normal (AppData)", "Portable (next to Fleet.exe)" }, wizPortable ? 1 : 0);
                        wizPortable = dm == 1;
                        break;
                    }
                default:
                    {
                        ui.Text("Add your accounts", x, y, Fonts.H1, Pal.Text);
                        ui.Para("Log in with a browser (Fleet reads the login for you), or paste cookies. You can also do this later on the Accounts page.", new RectangleF(x, y + 50, cw, 70), Fonts.Body, Pal.Muted);
                        if (ui.Button("wz.br", new RectangleF(x, y + 130, cw, 46), "Log in with a browser", Btn.Primary, "plus")) { FinishWizard(); BrowserAdd(); Go("Accounts"); }
                        if (ui.Button("wz.pa", new RectangleF(x, y + 186, cw, 46), "Paste cookies", Btn.Ghost, null)) { FinishWizard(); Go("Accounts"); AddAccounts(); }
                        break;
                    }
            }
            float by = card.Bottom - 70;
            if (wizStep > 0 && ui.Button("wz.back", new RectangleF(x, by, 110, 42), "Back", Btn.Ghost, null)) wizStep--;
            string next = wizStep == 3 ? "Skip for now" : "Next";
            if (ui.Button("wz.next", new RectangleF(card.Right - 36 - 150, by, 150, 42), next, wizStep == 3 ? Btn.Ghost : Btn.Primary, null))
            {
                if (wizStep == 3) FinishWizard(); else wizStep++;
            }
        }

        void FinishWizard()
        {
            Store.Cfg.FirstRunDone = true;
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
