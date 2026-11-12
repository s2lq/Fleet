using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fleet
{
    public partial class App
    {
        readonly Canvas canvas;
        public string page = "Overview";
        static readonly string[] Pages = { "Overview", "Accounts", "Launch", "Servers", "Layout", "Health", "Performance", "Tools", "Settings" };
        readonly Dictionary<string, ScrollState> scrolls = new Dictionary<string, ScrollState>();
        public readonly HashSet<string> selected = new HashSet<string>();
        int secondCount;
        string updateText = "";

        public App(Canvas c) { canvas = c; }

        ScrollState Scroll(string key)
        {
            ScrollState s;
            if (!scrolls.TryGetValue(key, out s)) { s = new ScrollState(); scrolls[key] = s; }
            return s;
        }

        public void StartUp()
        {
            Store.AutoBackup();
            if (Store.Cfg.CheckUpdates) CheckUpdate();
            if (Store.Cfg.HealthAuto) HealthLater();
        }

        public void EverySecond()
        {
            secondCount++;
            if (Store.Cfg.AutoTrim && Engine.Live.Count > 0 && secondCount % Math.Max(1, Store.Cfg.TrimEveryMin * 60) == 0) Engine.TrimMemory();
            if (secondCount % 5 == 0) { Perf.Apply(); Perf.Background(); }
            if (secondCount % 30 == 0) Perf.AntiAfk();
            if (secondCount % Math.Max(10, Store.Cfg.WatcherSec) == 0) Engine.WatcherTick();
            if (Store.Cfg.HealthAuto && secondCount % (Math.Max(1, Store.Cfg.HealthHours) * 3600) == 0) CheckAll(true);
            if (secondCount % 1800 == 5) RefreshRobuxAll();
        }

        void Go(string p) { page = p; }
        public void Navigate(int index) { if (index >= 0 && index < Pages.Length) page = Pages[index]; }
        public void Navigate(string name) { if (Array.IndexOf(Pages, name) >= 0) page = name; }

        async void CheckUpdate()
        {
            try { if (await Updater.Check()) updateText = Lang.F("Fleet {0} is available", Updater.Latest); }
            catch (Exception) { }
        }

        public void Render(Ui ui)
        {
            float topH = 76;
            DrawTopBar(ui, topH);
            float cw = Math.Min(1200, ui.W - 64);
            RectangleF area = new RectangleF((ui.W - cw) / 2, topH + 8, cw, ui.H - topH - 20);
            if (!Store.Cfg.FirstRunDone) { DrawWizard(ui, area); ui.Overlay(); return; }
            float fade = ui.Anim("page." + page, 1f, 0.2f);
            switch (page)
            {
                case "Overview": DrawOverview(ui, area); break;
                case "Accounts": DrawAccounts(ui, area); break;
                case "Launch": DrawLaunch(ui, area); break;
                case "Servers": DrawServers(ui, area); break;
                case "Layout": DrawLayout(ui, area); break;
                case "Health": DrawHealth(ui, area); break;
                case "Performance": DrawPerformance(ui, area); break;
                case "Tools": DrawTools(ui, area); break;
                default: DrawSettings(ui, area); break;
            }
            if (fade < 0.99f) ui.Fill(new RectangleF(0, topH, ui.W, ui.H - topH), 0, Color.FromArgb((int)((1 - fade) * 200), Pal.Bg.R, Pal.Bg.G, Pal.Bg.B));
            ui.Overlay();
        }

        void DrawTopBar(Ui ui, float h)
        {
            float lx = ui.W < 1150 ? 20 : 32;
            ui.Logo(new RectangleF(lx, 18, 40, 40));
            if (ui.W >= 1150) ui.Text("Fleet", lx + 52, 24, Fonts.Get(22, true), Pal.Text);

            Font f = Fonts.BodyB;
            float pad = ui.W < 1250 ? 24 : 32;
            float[] widths = Pages.Select(p => ui.Measure(p, f).Width + pad).ToArray();
            float total = widths.Sum() + 8;
            float leftEdge = ui.W >= 1150 ? 150 : 70;
            float x0 = Math.Max(leftEdge, (ui.W - total) / 2);
            float rightNeed = ui.W < 1250 ? 150 : 290;
            if (x0 + total > ui.W - rightNeed) x0 = Math.Max(leftEdge, ui.W - rightNeed - total);
            RectangleF bar = new RectangleF(x0, 18, total, 42);
            ui.Glass(bar, 21, 0);
            int cur = Array.IndexOf(Pages, page);
            float tx = bar.X + 4 + widths.Take(cur).Sum();
            float px = ui.Anim("nav.x", tx, 0.26f), pw = ui.Anim("nav.w", widths[cur], 0.26f);
            RectangleF pill = new RectangleF(px, bar.Y + 4, pw, 34);
            ui.Fill(pill, 17, Pal.Panel2);
            ui.Stroke(pill, 17, Pal.Line2, 1f);
            float x = bar.X + 4;
            for (int i = 0; i < Pages.Length; i++)
            {
                RectangleF cell = new RectangleF(x, bar.Y + 4, widths[i], 34);
                bool over = ui.Over(cell);
                if (over) ui.WantHand = true;
                ui.TextIn(Pages[i], cell, f, i == cur ? Pal.Text : Pal.Mix(Pal.Muted, Pal.Text, over ? 0.7f : 0f), StringAlignment.Center);
                if (ui.Pressed && over) ui.Active = "nav" + i;
                if (ui.Released && ui.Active == "nav" + i) { ui.Active = null; if (over && i != cur) { Go(Pages[i]); ui.Set("page." + Pages[i], 0f); } }
                x += widths[i];
            }

            string run = Lang.F("{0} running", Engine.Live.Count);
            float rw = ui.ChipWidth(run);
            ui.Chip(run, ui.W - 24 - rw, 28, Engine.Live.Count > 0 ? Pal.Good : Pal.Muted);
            if (ui.W >= 1250)
            {
                string mi = Engine.MultiActive ? "Multi-instance on" : "Multi-instance off";
                float mw = ui.ChipWidth(mi);
                ui.Chip(mi, ui.W - 24 - rw - 10 - mw, 28, Engine.MultiActive ? Pal.Text : Pal.Warn);
            }
        }

        // ------------------------------------------------------------ shared helpers
        void Title(Ui ui, RectangleF a, string title, string sub)
        {
            ui.Text(title, a.X, a.Y, Fonts.H1, Pal.Text);
            ui.Text(sub, a.X + 1, a.Y + 42, Fonts.Body, Pal.Muted);
        }

        void Label(Ui ui, string text, float x, float y) { ui.Text(Lang.T(text).ToUpper(), x, y, Fonts.SmallB, Pal.Muted); }

        static string Mem(long bytes) { return bytes >= (1L << 30) ? (bytes / (double)(1L << 30)).ToString("0.0") + " GB" : (bytes >> 20) + " MB"; }

        // a switch with a title and one line under it; returns the new value
        bool Tog(Ui ui, string id, string title, string sub, float x, float y, float w, bool val)
        {
            ui.Text(title, x, y, Fonts.BodyB, Pal.Text);
            if (!string.IsNullOrEmpty(sub)) ui.Text(Ui.Fit(ui, Lang.T(sub), Fonts.Small, w - 70), x, y + 20, Fonts.Small, Pal.Muted);
            return ui.Toggle(id, new RectangleF(x + w - 46, y + 2, 46, 26), val);
        }

        // "Place id" label + text field row
        string Field(Ui ui, string id, string label, string value, string hint, float x, float y, float w)
        {
            Label(ui, label, x, y);
            return ui.TextField(id, new RectangleF(x, y + 22, w, 42), value, hint);
        }

        public Target BuildTarget()
        {
            Settings c = Store.Cfg;
            Target t = new Target { Mode = c.LaunchMode };
            if (c.LaunchMode == "Private")
            {
                t.Place = Roblox.ParsePlace(c.LastPrivate);
                t.LinkCode = Roblox.ParseLinkCode(c.LastPrivate) ?? "";
                if (t.LinkCode.Length == 0) t.Mode = "Game";
            }
            else
            {
                t.Place = Roblox.ParsePlace(c.LastPlace);
                if (c.LaunchMode == "Server") t.Job = (c.LastJob ?? "").Trim();
                if (c.LaunchMode == "Server" && t.Job.Length == 0) t.Mode = "Game";
            }
            return t;
        }

        public void LaunchList(List<Account> list)
        {
            Target t = BuildTarget();
            foreach (Account a in list) Engine.BreakerClear(a);
            if (Store.Cfg.LaunchMode == "Follow") { FollowAndLaunch(list); return; }
            canvas.Post(delegate { Engine.LaunchMany(list, t); });
        }

        async void FollowAndLaunch(List<Account> list)
        {
            try
            {
                string name = (Store.Cfg.LastFollow ?? "").Trim();
                string id = await Roblox.UserIdByName(name);
                if (id == null) { Log.Add("Follow: no player called " + name + "."); return; }
                Account any = Store.Accounts.FirstOrDefault(a => a.Status == 1) ?? Store.Accounts.FirstOrDefault();
                if (any == null) return;
                Dictionary<long, Roblox.Presence2> pr = await Roblox.PresenceFull(any.Cookie, new List<long> { long.Parse(id) });
                Roblox.Presence2 p;
                if (!pr.TryGetValue(long.Parse(id), out p) || p.Type != 2 || p.PlaceId <= 0) { Log.Add("Follow: " + name + " is not in a game right now."); return; }
                Log.Add("Follow: joining " + name + " in place " + p.PlaceId + ".");
                await Engine.LaunchMany(list, new Target { Mode = p.GameId.Length > 10 ? "Server" : "Game", Place = p.PlaceId, Job = p.GameId });
            }
            catch (Exception ex) { Log.Add("Follow failed: " + ex.Message); }
        }

        // ------------------------------------------------------------ Overview
        void DrawOverview(Ui ui, RectangleF a)
        {
            Title(ui, a, "Overview", Store.Accounts.Count == 0 ? "Add an account to get started." : "Everything that is running right now.");
            float y = a.Y + 90;
            if (updateText.Length > 0)
            {
                RectangleF b = new RectangleF(a.X, y, a.Width, 54);
                ui.Glass(b, 12, 0);
                ui.Text(updateText, b.X + 22, b.Y + 17, Fonts.BodyB, Pal.Text);
                if (ui.Button("ov.upd", new RectangleF(b.Right - 22 - 150, b.Y + 9, 150, 36), Updater.ExeUrl.Length > 0 ? "Install and restart" : "Download", Btn.Primary, null)) InstallUpdate();
                y += 70;
            }
            float gap = 16, tw = (a.Width - gap * 3) / 4;
            double cpu = Engine.Live.Sum(r => r.Cpu);
            long ram = Engine.Live.Sum(r => r.Ram);
            Tile(ui, new RectangleF(a.X, y, tw, 104), "Accounts", Store.Accounts.Count.ToString(), "saved on this PC");
            Tile(ui, new RectangleF(a.X + (tw + gap), y, tw, 104), "Running", Engine.Live.Count.ToString(), "Roblox windows");
            Tile(ui, new RectangleF(a.X + (tw + gap) * 2, y, tw, 104), "CPU", cpu.ToString("0.0") + "%", "of the whole PC");
            Tile(ui, new RectangleF(a.X + (tw + gap) * 3, y, tw, 104), "Memory", Mem(ram), Engine.Live.Count > 0 ? Lang.F("about {0} each", Mem(ram / Engine.Live.Count)) : "used by Roblox");
            y += 104 + 20;

            float lw = Math.Min(440, a.Width * 0.4f);
            RectangleF left = new RectangleF(a.X, y, lw, 260);
            ui.Glass(left, 18, 0);
            ui.Text("Quick launch", left.X + 24, left.Y + 20, Fonts.H2, Pal.Text);
            Store.Cfg.LastPlace = Field(ui, "ov.place", "Place id or game link", Store.Cfg.LastPlace, "e.g. 920587237", left.X + 24, left.Y + 58, left.Width - 48);
            if (ui.Button("ov.all", new RectangleF(left.X + 24, left.Y + 142, left.Width - 48, 44), "Launch all accounts", Btn.Primary, "play") && Store.Accounts.Count > 0)
            {
                Store.Cfg.LaunchMode = "Game"; Store.SaveSettings();
                LaunchList(Store.Accounts.OrderBy(x => x.Order).ToList());
            }
            if (ui.Button("ov.stop", new RectangleF(left.X + 24, left.Y + 196, left.Width - 48, 40), "Stop everything", Btn.Ghost, "stop")) Engine.StopAll();

            RectangleF right = new RectangleF(a.X + lw + 16, y, a.Width - lw - 16, 260);
            ui.Glass(right, 18, 0);
            ui.Text("Running now", right.X + 24, right.Y + 20, Fonts.H2, Pal.Text);
            if (Engine.Live.Count == 0)
                ui.TextIn(Engine.Busy ? Engine.BusyText : "Nothing is running. Launch your accounts and they appear here.", new RectangleF(right.X + 24, right.Y + 60, right.Width - 48, 150), Fonts.Body, Pal.Muted, StringAlignment.Center);
            else
            {
                float ry = right.Y + 62;
                foreach (Running r in Engine.Live.Take(4))
                {
                    ui.Avatar(new RectangleF(right.X + 24, ry, 36, 36), r.Acc);
                    ui.Text(Ui.Fit(ui, r.Acc.Label, Fonts.BodyB, 200), right.X + 72, ry + 1, Fonts.BodyB, Pal.Text);
                    ui.Text(r.Cpu.ToString("0.0") + "% CPU   " + Mem(r.Ram), right.X + 72, ry + 20, Fonts.Small, Pal.Muted);
                    if (ui.Button("ov.k." + r.Acc.Id, new RectangleF(right.Right - 24 - 36, ry, 36, 36), "", Btn.Danger, "stop")) { Engine.Kill(r); break; }
                    ry += 46;
                }
                if (Engine.Live.Count > 4) ui.Text("+ " + (Engine.Live.Count - 4), right.X + 24, right.Bottom - 30, Fonts.Small, Pal.Muted);
            }

            if (Store.Accounts.Count == 0)
            {
                RectangleF cta = new RectangleF(a.X, y + 280, a.Width, 90);
                ui.Glass(cta, 18, 0);
                ui.Text("No accounts yet", cta.X + 24, cta.Y + 22, Fonts.H2, Pal.Text);
                ui.Text("Log in with a browser or paste cookies on the Accounts page.", cta.X + 24, cta.Y + 52, Fonts.Body, Pal.Muted);
                if (ui.Button("ov.add", new RectangleF(cta.Right - 24 - 170, cta.Y + 22, 170, 44), "Add accounts", Btn.Primary, "plus")) { Go("Accounts"); ui.Set("page.Accounts", 0f); }
            }
        }

        void Tile(Ui ui, RectangleF r, string label, string value, string sub)
        {
            ui.Glass(r, 18, 0);
            ui.Text(Lang.T(label).ToUpper(), r.X + 22, r.Y + 18, Fonts.SmallB, Pal.Muted);
            ui.Text(value, r.X + 22, r.Y + 38, Fonts.Get(30, true), Pal.Text);
            ui.Text(sub, r.X + 22, r.Y + 78, Fonts.Small, Pal.Faint);
        }

        async void InstallUpdate()
        {
            Log.Add("Installing the update...");
            string err = await Updater.Install();
            if (err != null) { Log.Add("Update failed: " + err); return; }
            Application.Exit();
        }
    }
}
