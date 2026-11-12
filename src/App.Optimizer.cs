using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fleet
{
    // Tools > Optimizer: a Windows cursor, a nicer font, a black and white interface and less leftover files in Roblox
    public partial class App
    {
        string optResult = "";
        DateTime optAt = DateTime.MinValue;
        List<string> optDirs = new List<string>();
        bool optAdmin = true;

        void OptRefresh()
        {
            optDirs = Optimizer.VersionDirs();
            optAdmin = !Optimizer.NeedsAdmin() || Optimizer.IsAdmin();
            optAt = DateTime.UtcNow;
        }

        void DrawOptimizer(Ui ui, RectangleF a)
        {
            Settings c = Store.Cfg;
            if ((DateTime.UtcNow - optAt).TotalSeconds > 6 && !Optimizer.Running) OptRefresh();
            RectangleF view = new RectangleF(a.X - 8, a.Y + 80, a.Width + 16, a.Bottom - a.Y - 80);
            float contentH = 1010;
            ui.BeginScroll(view, contentH, Scroll("opt"));
            RectangleF col = new RectangleF(a.X, view.Y + 4, a.Width, 0);
            float y = col.Y;

            // ---- install
            RectangleF s = Sect(ui, col, y, 190 + Math.Min(4, optDirs.Count) * 24, "Your Roblox");
            float x = s.X + 28, w = s.Width - 56, ry = s.Y + 60;
            if (optDirs.Count == 0) ui.Text("Roblox was not found. Choose its Versions folder (for example C:\\Program Files\\Roblox\\Versions).", x, ry, Fonts.Body, Pal.Warn);
            else ui.Text(optDirs.Count + " version" + (optDirs.Count == 1 ? "" : "s") + " found", x, ry, Fonts.BodyB, Pal.Text);
            ry += 28;
            foreach (string d in optDirs.Take(4))
            {
                ui.Text(Ui.Fit(ui, d, Fonts.Small, w - 120), x, ry, Fonts.Small, Pal.Muted);
                ry += 24;
            }
            ry += 8;
            if (ui.Button("op.folder", new RectangleF(x, ry, 190, 40), "Choose folder...", Btn.Ghost, null)) canvas.Post(delegate
            {
                using (FolderBrowserDialog d = new FolderBrowserDialog { Description = "Pick the Roblox Versions folder" })
                    if (d.ShowDialog(canvas.FindForm()) == DialogResult.OK) { Store.Cfg.RobloxFolder = d.SelectedPath; Store.SaveSettings(); Engine.ForgetRoblox(); OptRefresh(); }
            });
            if (!optAdmin && ui.Button("op.admin", new RectangleF(x + 202, ry, 270, 40), "Restart as administrator", Btn.Primary, null)) RestartAsAdmin();
            if (!optAdmin) ui.Text("Roblox is in Program Files, so Fleet needs administrator rights to change it.", x + 486, ry + 11, Fonts.Small, Pal.Warn);
            y = s.Bottom + 16;

            // ---- look
            s = Sect(ui, col, y, 438, "Look");
            x = s.X + 28; w = s.Width - 56; ry = s.Y + 60;
            c.OptCursor = Tog(ui, "op.cur", "Windows cursor", "Swaps Roblox's arrow and text cursors for the normal white Windows pointer.", x, ry, w, c.OptCursor); ry += 52;
            c.OptFont = Tog(ui, "op.font", "Nicer font", "Replaces the Gotham style text in menus and chat.", x, ry, w, c.OptFont); ry += 40;
            List<Optimizer.FontChoice> fonts = Optimizer.FontList();
            string[] names = fonts.Select(f => f.Name).ToArray();
            int fi = Math.Max(0, Array.IndexOf(names, c.OptFontName));
            if (names.Length > 0)
            {
                int nf = ui.Picker("op.fontpick", new RectangleF(x, ry, 280, 42), names, fi, "Font");
                if (nf != fi || (c.OptFontName != names[nf] && string.IsNullOrEmpty(c.OptFontFile))) { c.OptFontName = names[nf]; c.OptFontFile = ""; Save(); }
            }
            if (ui.Button("op.fontfile", new RectangleF(x + 292, ry, 190, 42), "Use a font file...", Btn.Ghost, null)) canvas.Post(delegate
            {
                using (OpenFileDialog d = new OpenFileDialog { Filter = "Fonts|*.ttf;*.otf" })
                    if (d.ShowDialog(canvas.FindForm()) == DialogResult.OK) { Store.Cfg.OptFontFile = d.FileName; Store.SaveSettings(); }
            });
            if (!string.IsNullOrEmpty(c.OptFontFile)) ui.Text(Ui.Fit(ui, Path.GetFileName(c.OptFontFile), Fonts.Small, w - 500), x + 496, ry + 12, Fonts.Small, Pal.Muted);
            ry += 62;
            c.OptMono = Tog(ui, "op.mono", "Black and white interface", "Removes the colour from the pictures the in-game menus use.", x, ry, w, c.OptMono); ry += 44;
            float half = w / 2 - 10;
            c.OptMonoMenu = Tog(ui, "op.m1", "Settings and menu", "", x + 20, ry, half - 20, c.OptMonoMenu);
            c.OptMonoEmotes = Tog(ui, "op.m2", "Emote wheel", "", x + w / 2 + 10, ry, half, c.OptMonoEmotes); ry += 38;
            c.OptMonoHud = Tog(ui, "op.m3", "Top bar, chat and player list", "", x + 20, ry, half - 20, c.OptMonoHud);
            c.OptMonoOther = Tog(ui, "op.m4", "Everything else", "", x + w / 2 + 10, ry, half, c.OptMonoOther); ry += 44;
            ui.Para("Only pictures change. Colours Roblox draws with code (some flat buttons and panels) stay as they are, and a Roblox update can bring them back until you apply again.", new RectangleF(x, ry, w, 50), Fonts.Small, Pal.Faint);
            y = s.Bottom + 16;

            // ---- cleaning
            s = Sect(ui, col, y, 270, "Clean up");
            x = s.X + 28; w = s.Width - 56; ry = s.Y + 60;
            c.OptCleanOld = Tog(ui, "op.old", "Remove old Roblox versions", "Roblox leaves every version it ever updated from. Keeps the newest.", x, ry, w, c.OptCleanOld); ry += 52;
            c.OptCleanLogs = Tog(ui, "op.logs", "Clear logs and temporary files", "Crash logs, finished downloads and the web cache.", x, ry, w, c.OptCleanLogs); ry += 52;
            c.OptCleanCache = Tog(ui, "op.cache", "Clear the asset cache", "Frees the most space, but games load slower the first time after.", x, ry, w, c.OptCleanCache);
            y = s.Bottom + 16;

            // ---- go
            s = Sect(ui, col, y, 210, "Apply");
            x = s.X + 28; w = s.Width - 56; ry = s.Y + 60;
            if (Optimizer.Running)
            {
                ui.Spinner(new RectangleF(x, ry + 8, 22, 22), Pal.A);
                ui.Text(Optimizer.Status, x + 34, ry + 8, Fonts.BodyB, Pal.Text);
                ui.Bar(new RectangleF(x, ry + 44, w, 8), ui.Anim("op.prog", Optimizer.Progress, 0.2f), Pal.A, Pal.A);
                ui.NeedFrames = true;
            }
            else
            {
                if (ui.Button("op.apply", new RectangleF(x, ry, 170, 44), "Apply", Btn.Primary, null)) { Save(); optResult = ""; Task.Run(delegate { optResult = Optimizer.Apply(); Log.Add("Optimizer: " + optResult); optAt = DateTime.MinValue; }); }
                if (ui.Button("op.restore", new RectangleF(x + 182, ry, 210, 44), "Restore originals", Btn.Ghost, null)) { Save(); Task.Run(delegate { int n = Optimizer.Restore(); optResult = "Put back " + n + " file(s)."; Log.Add("Optimizer: " + optResult); optAt = DateTime.MinValue; }); }
                if (optResult.Length > 0) ui.Text(Ui.Fit(ui, optResult, Fonts.Body, w - 420), x + 410, ry + 12, Fonts.Body, Pal.Muted);
                c.OptAuto = Tog(ui, "op.auto", "Apply again when Roblox updates", "Checks the version right before each launch.", x, ry + 62, w, c.OptAuto);
                ui.Text("Changing Roblox's files can stop working after an update and is at your own risk. Restore puts every changed file back.", x, ry + 106, Fonts.Small, Pal.Faint);
            }
            y = s.Bottom + 16;
            ui.EndScroll();
        }

        void RestartAsAdmin()
        {
            try
            {
                string exe = Application.ExecutablePath;
                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", "-NoProfile -WindowStyle Hidden -Command \"Start-Sleep 2; Start-Process -FilePath '" + exe.Replace("'", "''") + "' -Verb RunAs\"") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi);
                Application.Exit();
            }
            catch (Exception ex) { Log.Add("Could not restart as administrator: " + ex.Message); }
        }
    }
}
