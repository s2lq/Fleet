using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Fleet
{
    public partial class App
    {
        int setSub;
        string verStatus = "";
        List<Versions.Release> releases = new List<Versions.Release>();
        string[] pxTest = new string[0];

        void DrawSettings(Ui ui, RectangleF a)
        {
            Title(ui, a, "Settings", "Make Fleet yours.");
            string[] subs = { "General", "Appearance", "Network", "Data", "Roblox", "About" };
            setSub = ui.Segmented("st.sub", new RectangleF(Math.Max(a.X + 300, a.Right - 640), a.Y + 6, Math.Min(640, a.Width - 300), 40), subs, setSub);
            RectangleF view = new RectangleF(a.X - 8, a.Y + 82, a.Width + 16, a.Height - 82);
            RectangleF col = new RectangleF(a.X, view.Y + 8, Math.Min(a.Width, 860), 0);
            ScrollState sc = Scroll("settings" + setSub);
            float[] heights = { 1650, 840, 820, 700, 760, 260 };
            ui.BeginScroll(view, heights[setSub], sc);
            switch (setSub)
            {
                case 0: SetGeneral(ui, col); break;
                case 1: SetAppearance(ui, col); break;
                case 2: SetNetwork(ui, col); break;
                case 3: SetData(ui, col); break;
                case 4: SetRoblox(ui, col); break;
                default: SetAbout(ui, col); break;
            }
            ui.EndScroll();
        }

        // draws a rounded section card and returns its inner x/width
        RectangleF Sect(Ui ui, RectangleF col, float y, float h, string title)
        {
            RectangleF r = new RectangleF(col.X, y, col.Width, h);
            ui.Glass(r, 18, 0);
            ui.Text(title, r.X + 28, r.Y + 20, Fonts.H2, Pal.Text);
            return r;
        }

        void Save() { Store.SaveSettings(); }

        // ---------------------------------------------------------------- general
        void SetGeneral(Ui ui, RectangleF col)
        {
            Settings c = Store.Cfg;
            float y = col.Y;
            RectangleF s = Sect(ui, col, y, 500, "Window and system");
            float x = s.X + 28, w = s.Width - 56, ry = s.Y + 62;
            bool b;
            b = Tog(ui, "g.mi", "Multi-instance", "Hold Roblox's single-instance lock so several windows can run.", x, ry, w, c.MultiInstance); if (b != c.MultiInstance) { c.MultiInstance = b; Save(); Engine.SetMultiInstance(b); } ry += 52;
            b = Tog(ui, "g.tray", "Tray icon", "Keep a Fleet icon next to the clock.", x, ry, w, c.Tray); if (b != c.Tray) { c.Tray = b; Save(); canvas.ApplyShell(); } ry += 52;
            b = Tog(ui, "g.ct", "Close button hides to tray", "Use Quit in the tray menu to exit.", x, ry, w, c.CloseToTray); if (b != c.CloseToTray) { c.CloseToTray = b; if (b) c.Tray = true; Save(); canvas.ApplyShell(); } ry += 52;
            b = Tog(ui, "g.mt", "Minimize hides to tray", "", x, ry, w, c.MinToTray); if (b != c.MinToTray) { c.MinToTray = b; if (b) c.Tray = true; Save(); canvas.ApplyShell(); } ry += 52;
            b = Tog(ui, "g.toast", "Notifications", "Windows notices for crashes, kicks and relaunches.", x, ry, w, c.Toasts); if (b != c.Toasts) { c.Toasts = b; if (b) c.Tray = true; Save(); canvas.ApplyShell(); } ry += 52;
            b = Tog(ui, "g.hk", "Global hotkeys", "Ctrl+Alt+F5 launch all, F6 stop all, F7 show or hide.", x, ry, w, c.Hotkeys); if (b != c.Hotkeys) { c.Hotkeys = b; Save(); canvas.ApplyShell(); } ry += 52;
            b = Tog(ui, "g.start", "Start with Windows", "Opens Fleet minimized when you sign in.", x, ry, w, c.StartWithWindows); if (b != c.StartWithWindows) { c.StartWithWindows = b; Save(); Shell.SetStartup(b); } ry += 52;
            b = Tog(ui, "g.upd", "Check for updates", "Looks for a new release when Fleet starts.", x, ry, w, c.CheckUpdates); if (b != c.CheckUpdates) { c.CheckUpdates = b; Save(); } ry += 52;
            y = s.Bottom + 16;

            s = Sect(ui, col, y, 410, "Watcher");
            x = s.X + 28; w = s.Width - 56; ry = s.Y + 62;
            b = Tog(ui, "w.auto", "Auto-relaunch", "Bring an account back when its window closes or it is kicked.", x, ry, w, c.AutoRelaunch); if (b != c.AutoRelaunch) { c.AutoRelaunch = b; Save(); } ry += 52;
            b = Tog(ui, "w.same", "Rejoin the same server", "Try the server the account was in before.", x, ry, w, c.RejoinSame); if (b != c.RejoinSame) { c.RejoinSame = b; Save(); } ry += 52;
            b = Tog(ui, "w.br", "Crash breaker", "Stop relaunching an account that keeps crashing.", x, ry, w, c.Breaker); if (b != c.Breaker) { c.Breaker = b; Save(); } ry += 56;
            ui.Text(Lang.F("Check every {0} seconds", c.WatcherSec), x, ry, Fonts.BodyB, Pal.Text);
            int ws = ui.Slider("w.sec", new RectangleF(x, ry + 28, w, 24), c.WatcherSec, 20, 300); if (ws != c.WatcherSec) { c.WatcherSec = ws; Save(); } ry += 66;
            ui.Text(Lang.F("Pause {0} min after {1} crashes in 10 min", c.BreakerCooldownMin, c.BreakerMax), x, ry, Fonts.BodyB, Pal.Text);
            int bm = ui.Slider("w.bm", new RectangleF(x, ry + 28, w, 24), c.BreakerMax, 2, 8); if (bm != c.BreakerMax) { c.BreakerMax = bm; Save(); }
            y = s.Bottom + 16;

            s = Sect(ui, col, y, 300, "Discord alerts");
            x = s.X + 28; w = s.Width - 56; ry = s.Y + 62;
            c.Webhook = ui.TextField("d.url", new RectangleF(x, ry, w - 130, 42), c.Webhook, "Webhook URL (https://discord.com/api/webhooks/...)");
            if (ui.Button("d.test", new RectangleF(x + w - 120, ry, 120, 42), "Send test", Btn.Ghost, null)) { Save(); Hook.Send("Fleet", "Test message. Alerts are working.", Hook.Green); }
            ry += 58;
            b = Tog(ui, "d.crash", "A window closes unexpectedly", "", x, ry, w, c.HookCrash); if (b != c.HookCrash) { c.HookCrash = b; Save(); } ry += 40;
            b = Tog(ui, "d.rel", "An account is relaunched", "", x, ry, w, c.HookRelaunch); if (b != c.HookRelaunch) { c.HookRelaunch = b; Save(); } ry += 40;
            b = Tog(ui, "d.lock", "An account is locked", "", x, ry, w, c.HookLocked); if (b != c.HookLocked) { c.HookLocked = b; Save(); } ry += 40;
            b = Tog(ui, "d.batch", "A launch finishes", "", x, ry, w, c.HookBatch); if (b != c.HookBatch) { c.HookBatch = b; Save(); }
            y = s.Bottom + 16;

            s = Sect(ui, col, y, 250, "Discord Rich Presence");
            x = s.X + 28; w = s.Width - 56; ry = s.Y + 62;
            b = Tog(ui, "rp.on", "Show Fleet on my Discord profile", "Shows how many accounts are running.", x, ry, w, c.RichPresence); if (b != c.RichPresence) { c.RichPresence = b; Save(); Rpc.Update(Engine.Live.Count, Store.Accounts.Count); } ry += 58;
            c.DiscordAppId = ui.TextField("rp.id", new RectangleF(x, ry, w, 42), c.DiscordAppId, "Your Discord Application ID (a number)");
            ui.Text(Rpc.Status.Length > 0 ? Rpc.Status : "Make an app at discord.com/developers and paste its Application ID.", x, ry + 52, Fonts.Small, Pal.Muted);
        }

        // ---------------------------------------------------------------- appearance
        void SetAppearance(Ui ui, RectangleF col)
        {
            Settings c = Store.Cfg;
            RectangleF s = Sect(ui, col, col.Y, 780, "Appearance");
            float x = s.X + 28, w = s.Width - 56, ry = s.Y + 62;

            Label(ui, "Theme", x, ry);
            List<string> names = Themes.Names();
            int ti = Math.Max(0, names.IndexOf(c.Theme));
            int nti = ui.Picker("a.theme", new RectangleF(x, ry + 22, 260, 42), names.ToArray(), ti, "Theme");
            if (nti != ti) { c.Theme = names[nti]; Save(); Themes.Apply(c.Theme); }
            float bx = x + 272;
            if (ui.Button("a.edit", new RectangleF(bx, ry + 22, 120, 42), "Edit colours", Btn.Ghost, null)) EditTheme(); bx += 128;
            if (ui.Button("a.exp", new RectangleF(bx, ry + 22, 90, 42), "Export", Btn.Ghost, null)) canvas.Post(delegate
            {
                using (SaveFileDialog d = new SaveFileDialog { Filter = "Fleet theme|*.ftheme", FileName = c.Theme + ".ftheme" }) if (d.ShowDialog(canvas.FindForm()) == DialogResult.OK) { string e = Themes.Export(d.FileName); Log.Add(e ?? "Theme exported."); }
            }); bx += 98;
            if (ui.Button("a.imp", new RectangleF(bx, ry + 22, 90, 42), "Import", Btn.Ghost, null)) canvas.Post(delegate
            {
                using (OpenFileDialog d = new OpenFileDialog { Filter = "Fleet theme|*.ftheme|All files|*.*" }) if (d.ShowDialog(canvas.FindForm()) == DialogResult.OK) { string e = Themes.Import(d.FileName); Log.Add(e ?? "Theme imported."); }
            });
            ry += 84;
            if (c.CustomThemes.Any(t => t.Name == c.Theme))
            {
                if (ui.Button("a.del", new RectangleF(x, ry - 8, 160, 36), "Delete my theme", Btn.Danger, "trash")) { c.CustomThemes.RemoveAll(t => t.Name == c.Theme); c.Theme = "Black"; Themes.Apply("Black"); Save(); }
                ry += 44;
            }

            Label(ui, "Accent", x, ry);
            for (int i = 0; i < Pal.Accents.Length; i++)
            {
                RectangleF sw = new RectangleF(x + i * 52, ry + 26, 36, 36);
                bool over = ui.Over(sw);
                if (over) ui.WantHand = true;
                float grow = ui.Anim("sw" + i, over ? 1f : 0f, 0.3f);
                ui.Fill(new RectangleF(sw.X - grow * 2, sw.Y - grow * 2, sw.Width + grow * 4, sw.Height + grow * 4), 20, i == 0 ? Pal.Text : Pal.Accents[i].A);
                if (c.Accent == i) using (Pen pen = new Pen(Pal.Text, 2f)) ui.G.DrawEllipse(pen, sw.X - 4, sw.Y - 4, sw.Width + 8, sw.Height + 8);
                if (ui.Pressed && over) ui.Active = "sw" + i;
                if (ui.Released && ui.Active == "sw" + i) { ui.Active = null; if (over) { c.Accent = i; Save(); } }
            }
            ry += 84;

            Label(ui, "Wallpaper", x, ry);
            string[] wp = { "Dots", "Grid", "Off" };
            int wi = Math.Max(0, Array.IndexOf(wp, c.Wallpaper));
            int nwi = ui.Segmented("a.wp", new RectangleF(x, ry + 22, 360, 42), wp, wi);
            if (nwi != wi) { c.Wallpaper = wp[nwi]; Save(); }
            ry += 84;
            Label(ui, "Animation", x, ry);
            int mo = ui.Segmented("a.motion", new RectangleF(x, ry + 22, 380, 42), new string[] { "Efficient", "Smooth", "Minimal" }, c.Motion);
            if (mo != c.Motion) { c.Motion = mo; Save(); }
            ui.Text(c.Motion == 0 ? "Low CPU: the wallpaper moves slowly and everything pauses when Fleet is in the background." : (c.Motion == 1 ? "Smoother motion, a little more CPU." : "Quick transitions only, no wallpaper movement."), x, ry + 70, Fonts.Small, Pal.Faint);
            ry += 98;
            bool ps = Tog(ui, "a.ps", "Save power on battery", "Stops the wallpaper and pulses while unplugged.", x, ry, w, c.PowerSave); if (ps != c.PowerSave) { c.PowerSave = ps; Save(); } ry += 56;
            bool rm = Tog(ui, "a.rm", "Reduce motion", "Keeps the wallpaper still.", x, ry, w, c.ReduceMotion); if (rm != c.ReduceMotion) { c.ReduceMotion = rm; Save(); } ry += 56;

            Label(ui, "Language", x, ry);
            List<string[]> langs = Lang.Available();
            int li = Math.Max(0, langs.FindIndex(l => l[0] == c.Language));
            int nli = ui.Picker("a.lang", new RectangleF(x, ry + 22, 320, 42), langs.Select(l => l[1]).ToArray(), li, "Language");
            if (nli != li) { c.Language = langs[nli][0]; Save(); Lang.Load(c.Language); }
            ry += 84;

            bool cp = Tog(ui, "a.cp", "Compact mode", "Smaller bars and tighter spacing. Ctrl+Shift+K.", x, ry, w, c.Compact); if (cp != c.Compact) { c.Compact = cp; Save(); canvas.ApplyShell(); } ry += 56;
            if (ui.Button("a.mini", new RectangleF(x, ry, 200, 42), "Open the mini window", Btn.Ghost, null)) canvas.ShowMini();
        }

        void EditTheme()
        {
            canvas.Post(delegate
            {
                CustomTheme t = Store.Cfg.CustomThemes.FirstOrDefault(x => x.Name == Store.Cfg.Theme) ?? Themes.Current();
                string old = t.Name;
                if (!Dialogs.ThemeEditor(canvas.FindForm(), t)) return;
                if (Themes.Presets.Any(p => p.Name == t.Name)) t.Name += " (mine)";
                Store.Cfg.CustomThemes.RemoveAll(x => x.Name == old || x.Name == t.Name);
                Store.Cfg.CustomThemes.Add(t);
                Store.Cfg.Theme = t.Name; Store.SaveSettings(); Themes.Apply(t.Name);
            });
        }

        // ---------------------------------------------------------------- network
        void SetNetwork(Ui ui, RectangleF col)
        {
            Settings c = Store.Cfg;
            RectangleF s = Sect(ui, col, col.Y, 440, "Proxies");
            float x = s.X + 28, w = s.Width - 56, ry = s.Y + 62;
            int count = Proxies.Pool().Count;
            ui.Text(Lang.F("{0} proxies in the list. They are used for Fleet's own web requests per account, not for the Roblox game traffic.", count), x, ry, Fonts.Body, Pal.Muted);
            ry += 34;
            if (ui.Button("n.edit", new RectangleF(x, ry, 170, 42), "Edit the list", Btn.Primary, "edit")) EditProxies();
            if (ui.Button("n.test", new RectangleF(x + 180, ry, 170, 42), "Test proxies", Btn.Ghost, null)) TestProxies();
            ry += 62;
            Label(ui, "Accounts per proxy", x, ry);
            int[] per = { 1, 2, 3, 5, 10 };
            int pi = Math.Max(0, Array.IndexOf(per, c.ProxyPerAccounts));
            int npi = ui.Segmented("n.per", new RectangleF(x, ry + 22, 360, 42), new string[] { "1", "2", "3", "5", "10" }, pi);
            if (npi != pi) { c.ProxyPerAccounts = per[npi]; Save(); }
            ry += 84;
            bool pa = Tog(ui, "n.auto", "Give new accounts a proxy", "Handed out in order, the chosen number per proxy.", x, ry, w, c.ProxyAuto); if (pa != c.ProxyAuto) { c.ProxyAuto = pa; Save(); } ry += 62;
            if (ui.Button("n.all", new RectangleF(x, ry, 230, 42), "Assign to all accounts", Btn.Ghost, null)) { int n = Proxies.AssignAll(); Log.Add(n == 0 ? "Add proxies to the list first." : "Proxies assigned to " + n + " account(s)."); }
            if (ui.Button("n.rm", new RectangleF(x + 240, ry, 250, 42), "Remove automatic proxies", Btn.Danger, null)) Log.Add(Proxies.RemoveAuto() + " proxy assignment(s) removed.");
            ry += 62;
            foreach (string line in pxTest.Take(5)) { ui.Text(line, x, ry, Fonts.Small, Pal.Muted); ry += 20; }
            float py = s.Bottom + 16;
            RectangleF g = Sect(ui, col, py, 250, "Roblox itself (experimental)");
            bool ge = Tog(ui, "n.env", "Try to route Roblox through the account's proxy", "Starts Roblox directly with proxy settings in its environment.", g.X + 28, g.Y + 62, g.Width - 56, c.GameProxyEnv);
            if (ge != c.GameProxyEnv) { c.GameProxyEnv = ge; Save(); }
            ui.Para("Only web requests that Roblox sends through the system proxy settings are covered, and only if it honours them. Gameplay traffic is UDP and is not proxied by this. For full coverage run each account behind its own VPN or a proxifier tool.", new RectangleF(g.X + 28, g.Y + 118, g.Width - 56, 110), Fonts.Small, Pal.Muted);
        }

        void EditProxies()
        {
            canvas.Post(delegate
            {
                string v = Dialogs.Paste(canvas.FindForm(), Lang.T("Proxy list"), Lang.T("One proxy per line: host:port or host:port:user:pass"), Lang.T("Lines starting with # are ignored."), Store.Cfg.ProxyPool);
                if (v == null) return;
                Store.Cfg.ProxyPool = v.Replace("\r", ""); Store.SaveSettings();
            });
        }

        async void TestProxies()
        {
            List<string> pool = Proxies.Pool();
            List<string> res = new List<string>();
            pxTest = new string[] { "Testing..." };
            foreach (string p in pool.Take(5))
            {
                int ms = await Proxies.Test(p);
                res.Add(p.Split(':')[0] + ": " + (ms < 0 ? "failed" : ms + " ms"));
                pxTest = res.ToArray();
            }
            if (pool.Count == 0) pxTest = new string[] { "The proxy list is empty." };
        }

        // ---------------------------------------------------------------- data
        void SetData(Ui ui, RectangleF col)
        {
            RectangleF s = Sect(ui, col, col.Y, 250, "Protection");
            float x = s.X + 28, w = s.Width - 56, ry = s.Y + 62;
            ui.Text(Store.Password != null ? "Accounts file: protected with your own password." : "Accounts file: encrypted with your Windows account.", x, ry, Fonts.Body, Pal.Muted); ry += 38;
            if (ui.Button("p.set", new RectangleF(x, ry, 210, 42), "Set or change password", Btn.Primary, null)) SetPassword();
            if (ui.Button("p.rm", new RectangleF(x + 220, ry, 230, 42), "Use Windows encryption", Btn.Ghost, null)) RemovePassword();
            ry += 62;
            ui.Text(Store.IsPortable ? "Portable mode is on: data lives next to Fleet.exe." : "Normal mode: data lives in your AppData folder.", x, ry, Fonts.Body, Pal.Muted);
            float py = s.Bottom + 16;

            s = Sect(ui, col, py, 120, "Portable mode");
            x = s.X + 28;
            if (ui.Button("p.port", new RectangleF(x, s.Y + 62, 280, 42), Store.IsPortable ? "Switch back to normal mode" : "Switch to portable mode", Btn.Ghost, null)) TogglePortable();
            py = s.Bottom + 16;

            s = Sect(ui, col, py, 220, "Backups and moving PCs");
            x = s.X + 28; ry = s.Y + 62;
            if (ui.Button("b.now", new RectangleF(x, ry, 160, 42), "Back up now", Btn.Ghost, null)) { string f = Store.Backup(); Log.Add(f != null ? "Backup saved: " + Path.GetFileName(f) : "Nothing to back up yet."); }
            if (ui.Button("b.res", new RectangleF(x + 170, ry, 190, 42), "Restore a backup", Btn.Ghost, null)) RestoreBackup();
            ry += 54;
            if (ui.Button("b.exp", new RectangleF(x, ry, 230, 42), "Export all accounts", Btn.Ghost, null)) ExportTransfer();
            if (ui.Button("b.imp", new RectangleF(x + 240, ry, 230, 42), "Import accounts", Btn.Ghost, null)) ImportTransfer();
            py = s.Bottom + 16;

            s = Sect(ui, col, py, 150, "Folder and reset");
            x = s.X + 28; ry = s.Y + 62;
            if (ui.Button("o.imp", new RectangleF(x + 460, ry, 260, 42), "Import from another manager", Btn.Ghost, null)) ImportFromManager();
            if (ui.Button("o.open", new RectangleF(x, ry, 200, 42), "Open data folder", Btn.Ghost, null)) { try { Process.Start(new ProcessStartInfo(Store.Dir) { UseShellExecute = true }); } catch (Exception ex) { Log.Add(ex.Message); } }
            if (ui.Button("o.wipe", new RectangleF(x + 210, ry, 240, 42), "Remove all accounts", Btn.Danger, "trash")) canvas.Post(delegate
            {
                if (Dialogs.Confirm(canvas.FindForm(), "Remove all accounts", "Remove every saved account from Fleet?", "Remove all")) { Engine.StopAll(); Store.Accounts.Clear(); selected.Clear(); Store.SaveAccounts(); Log.Add("All accounts removed."); }
            });
            ui.Text(Ui.Fit(ui, Store.Dir, Fonts.Small, w), x, ry + 54, Fonts.Small, Pal.Faint);
        }

        void SetPassword()
        {
            canvas.Post(delegate
            {
                string pw = Dialogs.Password(canvas.FindForm(), Lang.T("Set a password"), Lang.T("Choose a password for your accounts. If you forget it they cannot be recovered."), true);
                if (pw == null) return;
                Store.Password = pw; Store.SaveAccounts(); Log.Add("Password set. Fleet asks for it every time it starts.");
            });
        }

        void RemovePassword()
        {
            if (Store.Password == null) { Log.Add("Already using Windows encryption."); return; }
            canvas.Post(delegate
            {
                string msg = Store.IsPortable ? "You are in portable mode: without a password anyone who copies the FleetData folder can read your accounts. I accept the risk." : "Remove the password and encrypt with your Windows account instead?";
                if (!Dialogs.Confirm(canvas.FindForm(), "Remove password", Lang.T(msg), "Remove")) return;
                Store.Password = null; Store.SaveAccounts(); Log.Add("Password removed.");
            });
        }

        // portable data can be copied with the folder, so ask for a password unless the user accepts the risk
        bool EnsurePortablePassword()
        {
            if (Store.Password != null) return true;
            if (Dialogs.Confirm(canvas.FindForm(), "Portable mode", Lang.T("Anyone who copies the FleetData folder could read your accounts. Set a password to protect them?"), "Set a password"))
            {
                string pw = Dialogs.Password(canvas.FindForm(), Lang.T("Set a password"), Lang.T("Choose a password for your accounts."), true);
                if (pw == null) return false;
                Store.Password = pw; Store.SaveAccounts();
                return true;
            }
            return Dialogs.Confirm(canvas.FindForm(), "Accept the risk", Lang.T("Continue without a password? Anyone with the folder can use your accounts. I accept the risk."), "Continue anyway");
        }

        void TogglePortable()
        {
            canvas.Post(delegate
            {
                bool on = !Store.IsPortable;
                if (on && !EnsurePortablePassword()) return;
                string err = Store.SetPortable(on);
                if (err != null) { Log.Add("Could not switch: " + err); return; }
                Application.Restart();
            });
        }

        void RestoreBackup()
        {
            canvas.Post(delegate
            {
                string dir = Path.Combine(Store.Dir, "Backups");
                using (OpenFileDialog d = new OpenFileDialog { Filter = "Fleet backup|*.fdat|All files|*.*", InitialDirectory = Directory.Exists(dir) ? dir : Store.Dir })
                    if (d.ShowDialog(canvas.FindForm()) == DialogResult.OK) Log.Add(Store.RestoreFrom(d.FileName) ?? "Backup restored.");
            });
        }

        void ExportTransfer()
        {
            canvas.Post(delegate
            {
                string pw = Dialogs.Password(canvas.FindForm(), Lang.T("Export accounts"), Lang.T("Password for the file:"), true);
                if (pw == null) return;
                using (SaveFileDialog d = new SaveFileDialog { Filter = "Fleet accounts|*.fleet", FileName = "accounts.fleet" })
                    if (d.ShowDialog(canvas.FindForm()) == DialogResult.OK) { try { Store.ExportTransfer(d.FileName, pw); Log.Add("Accounts exported."); } catch (Exception ex) { Log.Add("Export failed: " + ex.Message); } }
            });
        }

        void ImportTransfer()
        {
            canvas.Post(delegate
            {
                using (OpenFileDialog d = new OpenFileDialog { Filter = "Fleet accounts|*.fleet|All files|*.*" })
                {
                    if (d.ShowDialog(canvas.FindForm()) != DialogResult.OK) return;
                    string pw = Dialogs.Password(canvas.FindForm(), Lang.T("Import accounts"), Lang.T("Password of the file:"), false);
                    if (pw == null) return;
                    try { Log.Add(Store.ImportTransfer(d.FileName, pw) + " account(s) imported."); }
                    catch (Exception ex) { Log.Add("Import failed: " + ex.Message); }
                }
            });
        }

        // ---------------------------------------------------------------- roblox
        void SetRoblox(Ui ui, RectangleF col)
        {
            Settings c = Store.Cfg;
            RectangleF s = Sect(ui, col, col.Y, 700, "Roblox version");
            float x = s.X + 28, w = s.Width - 56, ry = s.Y + 62;
            string inst = Engine.FindRoblox();
            ui.Text("Installed: " + (inst != null ? Path.GetFileName(Path.GetDirectoryName(inst)) : "not found"), x, ry, Fonts.Body, Pal.Text); ry += 26;
            ui.Text("In use: " + (string.IsNullOrEmpty(c.RobloxVersion) ? "the one Roblox keeps updated" : c.RobloxVersion), x, ry, Fonts.Body, Pal.Muted); ry += 40;
            List<string> mine = Versions.Installed();
            string[] opts = new string[] { "Roblox's own (newest)" }.Concat(mine).ToArray();
            int cur = string.IsNullOrEmpty(c.RobloxVersion) ? 0 : Math.Max(0, mine.IndexOf(c.RobloxVersion) + 1);
            int nc = ui.Picker("r.use", new RectangleF(x, ry, w, 42), opts, cur, "Version to use");
            if (nc != cur) { c.RobloxVersion = nc == 0 ? "" : mine[nc - 1]; Save(); Log.Add(nc == 0 ? "Using the installed Roblox." : "Using " + c.RobloxVersion + " (direct launch)."); }
            ry += 56;
            if (ui.Button("r.list", new RectangleF(x, ry, 230, 42), "Show recent releases", Btn.Ghost, "refresh")) LoadReleases();
            ui.Text(Ui.Fit(ui, verStatus, Fonts.Small, w - 250), x + 242, ry + 14, Fonts.Small, Pal.Muted);
            ry += 58;
            foreach (Versions.Release r in releases.Take(8))
            {
                bool have = mine.Contains(r.Version);
                ui.Text(r.Version, x, ry + 8, Fonts.Body, Pal.Text);
                ui.Text(r.Date, x + 270, ry + 8, Fonts.Small, Pal.Muted);
                if (ui.Button("r.d" + r.Version, new RectangleF(x + w - 130, ry, 130, 34), have ? "Downloaded" : "Download", have ? Btn.Ghost : Btn.Primary, null) && !have) DownloadVersion(r.Version);
                ry += 42;
            }
            ui.Text("Old versions usually cannot join games anymore. Roblox forces updates.", x, s.Bottom - 40, Fonts.Small, Pal.Faint);
        }

        async void LoadReleases()
        {
            verStatus = "Loading...";
            try { releases = await Versions.Recent(); verStatus = releases.Count + " releases"; }
            catch (Exception ex) { verStatus = "Could not load: " + ex.Message; }
        }

        async void DownloadVersion(string v)
        {
            verStatus = "Downloading " + v + "...";
            string err = await Versions.Download(v, delegate(string t) { verStatus = t; });
            verStatus = err ?? "Downloaded " + v + ".";
            if (err == null) { Store.Cfg.RobloxVersion = v; Store.SaveSettings(); }
        }

        // ---------------------------------------------------------------- about
        void SetAbout(Ui ui, RectangleF col)
        {
            RectangleF s = new RectangleF(col.X, col.Y, col.Width, 220);
            ui.Glass(s, 18, 0);
            ui.Logo(new RectangleF(s.X + 28, s.Y + 28, 64, 64));
            ui.Text("Fleet " + Program.Version, s.X + 112, s.Y + 30, Fonts.H2, Pal.Text);
            ui.Text("An independent Roblox multi-account launcher. Not affiliated with or endorsed by Roblox.", s.X + 112, s.Y + 62, Fonts.Body, Pal.Muted);
            if (ui.Button("ab.upd", new RectangleF(s.X + 28, s.Y + 120, 220, 42), "Check for updates", Btn.Primary, "refresh")) CheckUpdateNow();
            if (updateText.Length > 0) ui.Text(updateText, s.X + 262, s.Y + 132, Fonts.BodyB, Pal.Good);
            bool au = Tog(ui, "ab.auto", "Install updates automatically", "Downloads, checks the SHA-256 and restarts.", s.X + 28, s.Y + 176, s.Width - 56, Store.Cfg.AutoInstall);
            if (au != Store.Cfg.AutoInstall) { Store.Cfg.AutoInstall = au; Store.SaveSettings(); }
        }

        async void CheckUpdateNow()
        {
            try { updateText = await Updater.Check() ? Lang.F("Fleet {0} is available", Updater.Latest) : "You have the latest version."; }
            catch (Exception) { updateText = "Could not check."; }
        }
    }
}
