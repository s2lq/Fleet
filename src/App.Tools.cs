using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fleet
{
    public partial class App
    {
        int toolSub;

        void DrawTools(Ui ui, RectangleF a)
        {
            Title(ui, a, "Tools", "Presets, shared configs, macros and the activity log.");
            toolSub = ui.Segmented("tl.sub", new RectangleF(a.Right - 480, a.Y + 6, 480, 40), new string[] { "Presets", "Configs", "Macro", "Activity" }, toolSub);
            if (toolSub == 0) DrawPresets(ui, a);
            else if (toolSub == 1) DrawConfigs(ui, a);
            else if (toolSub == 2) DrawMacro(ui, a);
            else DrawActivity(ui, a);
        }

        // ---------------------------------------------------------------- presets
        public void ApplyPreset(Preset p)
        {
            selected.Clear();
            foreach (string id in p.AccountIds) if (Store.Find(id) != null) selected.Add(id);
            Settings c = Store.Cfg;
            c.LaunchMode = p.Mode; c.LastPlace = p.Place; c.LastJob = p.Job; c.LastPrivate = p.Private; c.LayoutCols = p.Cols; c.LayoutGap = p.Gap; c.OnlySelected = true;
            Store.SaveSettings();
        }

        public async void RunPreset(Preset p)
        {
            ApplyPreset(p);
            List<Account> l = SelectedList();
            if (l.Count == 0) { Log.Add("The preset has no accounts left."); return; }
            LaunchList(l);
            while (Engine.Busy || Engine.Live.Count < 1) { await System.Threading.Tasks.Task.Delay(1500); if (!Engine.Busy) break; }
            await System.Threading.Tasks.Task.Delay(8000);
            Engine.Arrange(Store.Cfg.LayoutMonitor, Store.Cfg.LayoutCols, Store.Cfg.LayoutGap);
        }

        void DrawPresets(Ui ui, RectangleF a)
        {
            float by = a.Y + 84;
            if (ui.Button("pr.save", new RectangleF(a.X, by, 230, 42), "Save current setup", Btn.Primary, "plus")) SavePreset();
            ui.Text("A preset remembers the accounts, the game and the layout.", a.X + 246, by + 12, Fonts.Body, Pal.Muted);
            RectangleF view = new RectangleF(a.X - 8, by + 58, a.Width + 16, a.Bottom - by - 58);
            if (Store.Presets.Count == 0) { ui.TextIn("No presets yet.", view, Fonts.Body, Pal.Muted, StringAlignment.Center); return; }
            float rh = 76;
            ui.BeginScroll(view, Store.Presets.Count * (rh + 10) + 10, Scroll("presets"));
            float y = view.Y + 4;
            foreach (Preset p in Store.Presets.ToList())
            {
                RectangleF r = new RectangleF(a.X, y, a.Width, rh);
                ui.Glass(r, 14, 0);
                ui.Text(Ui.Fit(ui, p.Name, Fonts.BodyB, 300), r.X + 22, r.Y + 14, Fonts.BodyB, Pal.Text);
                ui.Text(Lang.F("{0} account(s)", p.AccountIds.Count) + "   " + p.Mode + (p.Place.Length > 0 ? "   " + p.Place : ""), r.X + 22, r.Y + 40, Fonts.Small, Pal.Muted);
                float bx = r.Right - 16;
                bx -= 36; if (ui.Button("pr.d" + p.Name, new RectangleF(bx, r.Y + 20, 36, 36), "", Btn.Quiet, "trash")) { Store.Presets.Remove(p); if (Store.Cfg.StartupPreset == p.Name) Store.Cfg.StartupPreset = ""; Store.SavePresets(); Store.SaveSettings(); break; }
                bx -= 112; if (ui.Button("pr.r" + p.Name, new RectangleF(bx, r.Y + 20, 104, 36), "Launch", Btn.Primary, "play")) RunPreset(p);
                bx -= 100; if (ui.Button("pr.a" + p.Name, new RectangleF(bx, r.Y + 20, 92, 36), "Apply", Btn.Ghost, null)) ApplyPreset(p);
                bool st = Store.Cfg.StartupPreset == p.Name;
                bx -= 150; if (ui.Button("pr.s" + p.Name, new RectangleF(bx, r.Y + 20, 142, 36), st ? "At startup: on" : "At startup: off", st ? Btn.Primary : Btn.Ghost, null)) { Store.Cfg.StartupPreset = st ? "" : p.Name; Store.SaveSettings(); }
                y += rh + 10;
            }
            ui.EndScroll();
        }

        void SavePreset()
        {
            canvas.Post(delegate
            {
                string name = Dialogs.Prompt(canvas.FindForm(), Lang.T("Save current setup"), Lang.T("Name of the preset:"), "");
                if (string.IsNullOrWhiteSpace(name)) return;
                name = name.Trim();
                List<Account> src = selected.Count > 0 ? SelectedList() : Store.Accounts.ToList();
                Settings c = Store.Cfg;
                Store.Presets.RemoveAll(p => p.Name == name);
                Store.Presets.Add(new Preset { Name = name, AccountIds = src.Select(s => s.Id).ToList(), Mode = c.LaunchMode, Place = c.LastPlace, Job = c.LastJob, Private = c.LastPrivate, Cols = c.LayoutCols, Gap = c.LayoutGap });
                Store.SavePresets();
            });
        }

        // ---------------------------------------------------------------- configs
        void DrawConfigs(Ui ui, RectangleF a)
        {
            float by = a.Y + 84;
            if (ui.Button("cf.exp", new RectangleF(a.X, by, 190, 42), "Export my config", Btn.Ghost, null)) ExportConfig();
            if (ui.Button("cf.imp", new RectangleF(a.X + 200, by, 190, 42), "Import a config", Btn.Ghost, null)) ImportConfig();
            ui.Text("Configs hold settings only. They never contain accounts, cookies or servers.", a.X + 406, by + 12, Fonts.Body, Pal.Muted);
            RectangleF view = new RectangleF(a.X - 8, by + 58, a.Width + 16, a.Bottom - by - 58);
            List<Config> all = Configs.Quick.Concat(Configs.Community).ToList();
            float rh = 74;
            ui.BeginScroll(view, all.Count * (rh + 8) + 80, Scroll("configs"));
            float y = view.Y + 4;
            for (int i = 0; i < all.Count; i++)
            {
                if (i == 0) { ui.Text("Quick configs", a.X, y, Fonts.H2, Pal.Text); y += 34; }
                if (i == Configs.Quick.Count) { y += 10; ui.Text("Community configs", a.X, y, Fonts.H2, Pal.Text); y += 34; }
                Config cf = all[i];
                RectangleF r = new RectangleF(a.X, y, a.Width, rh);
                ui.Glass(r, 14, 0);
                ui.Text(cf.Name, r.X + 22, r.Y + 13, Fonts.BodyB, Pal.Text);
                ui.Text(cf.Tagline, r.X + 22 + ui.Measure(cf.Name, Fonts.BodyB).Width + 14, r.Y + 15, Fonts.Small, Pal.Muted);
                ui.Text(Ui.Fit(ui, cf.Text, Fonts.Small, r.Width - 230), r.X + 22, r.Y + 42, Fonts.Small, Pal.Muted);
                if (ui.Button("cf." + cf.Name, new RectangleF(r.Right - 16 - 110, r.Y + 19, 110, 36), "Apply", Btn.Primary, null)) ApplyConfig(cf);
                y += rh + 8;
            }
            ui.EndScroll();
        }

        void ApplyConfig(Config cf)
        {
            canvas.Post(delegate
            {
                if (!Dialogs.Confirm(canvas.FindForm(), cf.Name, cf.Text + "\n\n" + Lang.T("This changes your graphics, frame rate, memory and CPU settings."), "Apply")) return;
                cf.Apply(Store.Cfg);
                Store.SaveSettings();
                Perf.CapChanged();
                Log.Add("Config applied: " + cf.Name + ".");
            });
        }

        void ExportConfig()
        {
            canvas.Post(delegate
            {
                using (SaveFileDialog d = new SaveFileDialog { Filter = "Fleet config|*.fcfg", FileName = "my-config.fcfg" })
                    if (d.ShowDialog(canvas.FindForm()) == DialogResult.OK) { Configs.Export(d.FileName, System.IO.Path.GetFileNameWithoutExtension(d.FileName)); Log.Add("Config exported."); }
            });
        }

        void ImportConfig()
        {
            canvas.Post(delegate
            {
                using (OpenFileDialog d = new OpenFileDialog { Filter = "Fleet config|*.fcfg|All files|*.*" })
                    if (d.ShowDialog(canvas.FindForm()) == DialogResult.OK)
                    {
                        string name; string err = Configs.Import(d.FileName, out name);
                        if (err != null) Log.Add("Could not import: " + err);
                        else { Themes.Apply(Store.Cfg.Theme); Perf.CapChanged(); Log.Add("Config imported" + (name.Length > 0 ? ": " + name : "") + "."); }
                    }
            });
        }

        // ---------------------------------------------------------------- macro
        void DrawMacro(Ui ui, RectangleF a)
        {
            RectangleF card = new RectangleF(a.X, a.Y + 90, Math.Min(760, a.Width), 330);
            ui.Glass(card, 18, 0);
            float px = card.X + 28;
            ui.Text("Macro recorder", px, card.Y + 22, Fonts.H2, Pal.Text);
            ui.Text("Records your mouse and keyboard, then repeats them (like TinyTask).", px, card.Y + 52, Fonts.Body, Pal.Muted);
            string state = Macro.Recording ? "Recording..." : (Macro.Playing ? "Playing..." : Lang.F("{0} steps recorded", Macro.Steps.Count));
            ui.Text(state, px, card.Y + 86, Fonts.BodyB, Macro.Recording ? Pal.Bad : Pal.Text);
            float by = card.Y + 120;
            if (!Macro.Recording) { if (ui.Button("mc.rec", new RectangleF(px, by, 150, 42), "Record", Btn.Primary, "play")) Macro.Record(); }
            else if (ui.Button("mc.stop", new RectangleF(px, by, 150, 42), "Stop recording", Btn.Danger, "stop")) Macro.StopRecord();
            if (!Macro.Playing) { if (ui.Button("mc.play", new RectangleF(px + 160, by, 130, 42), "Play", Btn.Ghost, "play")) Macro.Play(Store.Cfg.MacroLoops); }
            else if (ui.Button("mc.stopp", new RectangleF(px + 160, by, 130, 42), "Stop", Btn.Danger, "stop")) Macro.StopPlay();
            ui.Text(Store.Cfg.MacroLoops == 0 ? "Repeat: until stopped" : Lang.F("Repeat: {0} time(s)", Store.Cfg.MacroLoops), px, by + 66, Fonts.Body, Pal.Muted);
            int lp = ui.Slider("mc.loops", new RectangleF(px, by + 96, card.Width - 56, 24), Store.Cfg.MacroLoops, 0, 100);
            if (lp != Store.Cfg.MacroLoops) { Store.Cfg.MacroLoops = lp; Store.SaveSettings(); }
            if (ui.Button("mc.save", new RectangleF(px, by + 140, 130, 40), "Save", Btn.Ghost, null)) canvas.Post(delegate
            {
                using (SaveFileDialog d = new SaveFileDialog { Filter = "Fleet macro|*.fmacro", FileName = "macro.fmacro" }) if (d.ShowDialog(canvas.FindForm()) == DialogResult.OK) Macro.Save(d.FileName);
            });
            if (ui.Button("mc.load", new RectangleF(px + 140, by + 140, 130, 40), "Load", Btn.Ghost, null)) canvas.Post(delegate
            {
                using (OpenFileDialog d = new OpenFileDialog { Filter = "Fleet macro|*.fmacro|All files|*.*" }) if (d.ShowDialog(canvas.FindForm()) == DialogResult.OK) { try { Macro.Load(d.FileName); } catch (Exception ex) { Log.Add("Could not load: " + ex.Message); } }
            });
            ui.Text("Hotkeys: Ctrl+Alt+F8 record or stop, Ctrl+Alt+F9 play or stop.", card.X + 28, card.Bottom - 34, Fonts.Small, Pal.Faint);
        }

        // ---------------------------------------------------------------- activity
        void DrawActivity(Ui ui, RectangleF a)
        {
            if (ui.Button("act.clear", new RectangleF(a.Right - 130, a.Y + 84, 130, 40), "Clear", Btn.Ghost, "x")) lock (Log.Lines) Log.Lines.Clear();
            RectangleF box = new RectangleF(a.X, a.Y + 134, a.Width, a.Height - 134);
            ui.Glass(box, 18, 0);
            List<Log.Entry> copy;
            lock (Log.Lines) copy = Log.Lines.AsEnumerable().Reverse().ToList();
            if (copy.Count == 0) { ui.TextIn("Nothing yet.", box, Fonts.Body, Pal.Muted, StringAlignment.Center); return; }
            RectangleF view = new RectangleF(box.X, box.Y + 8, box.Width, box.Height - 16);
            ui.BeginScroll(view, copy.Count * 30 + 16, Scroll("activity"));
            float y = view.Y + 8;
            foreach (Log.Entry e in copy)
            {
                ui.Text(e.At.ToString("HH:mm:ss"), box.X + 26, y + 3, Fonts.Small, Pal.Faint);
                ui.Text(Ui.Fit(ui, e.Text, Fonts.Body, box.Width - 160), box.X + 110, y + 1, Fonts.Body, Pal.Text);
                y += 30;
            }
            ui.EndScroll();
        }
    }
}
