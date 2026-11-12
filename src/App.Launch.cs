using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Fleet
{
    public partial class App
    {
        static readonly string[] Modes = { "Game", "Server", "Private", "Follow" };
        string gameQuery = "";
        List<Roblox.GameHit> gameHits = new List<Roblox.GameHit>();
        string gameStatus = "";

        async void SearchGame()
        {
            string q = gameQuery.Trim();
            if (q.Length < 2) return;
            gameStatus = "Searching...";
            try { gameHits = await Roblox.SearchGames(q); gameStatus = gameHits.Count == 0 ? "No game found." : ""; }
            catch (Exception ex) { gameStatus = "Search failed: " + ex.Message; }
        }

        void Remember(long place)
        {
            if (place <= 0) return;
            string s = place.ToString();
            Store.Cfg.Recent.Remove(s);
            Store.Cfg.Recent.Insert(0, s);
            if (Store.Cfg.Recent.Count > 6) Store.Cfg.Recent.RemoveRange(6, Store.Cfg.Recent.Count - 6);
            Store.SaveSettings();
        }

        void DrawLaunch(Ui ui, RectangleF a)
        {
            Title(ui, a, "Launch", "Choose where your accounts go, then start them.");
            RectangleF view = new RectangleF(a.X - 8, a.Y + 82, a.Width + 16, a.Height - 82);
            ui.BeginScroll(view, 780, Scroll("launch"));
            float y = view.Y + 8;
            float lw = Math.Min(640, a.Width * 0.58f);
            RectangleF card = new RectangleF(a.X, y, lw, 540);
            ui.Glass(card, 18, 0);
            float px = card.X + 28, pw = card.Width - 56;

            Label(ui, "Destination", px, card.Y + 22);
            int mode = Math.Max(0, Array.IndexOf(Modes, Store.Cfg.LaunchMode));
            int nm = ui.Segmented("ln.mode", new RectangleF(px, card.Y + 44, pw, 42), new string[] { "Any server", "Exact server", "Private", "Follow" }, mode);
            if (nm != mode) { Store.Cfg.LaunchMode = Modes[nm]; Store.SaveSettings(); }

            float fy = card.Y + 108;
            string m = Store.Cfg.LaunchMode;
            if (m == "Private")
            {
                string[] names = Store.Servers.Select(s => s.Name).ToArray();
                if (names.Length > 0)
                {
                    int pick = ui.Picker("ln.sv", new RectangleF(px, fy, pw, 42), names, -1, "Pick a saved private server");
                    if (pick >= 0) Store.Cfg.LastPrivate = Store.Servers[pick].Link;
                    fy += 54;
                }
                Store.Cfg.LastPrivate = Field(ui, "ln.priv", "Private server link", Store.Cfg.LastPrivate, "https://www.roblox.com/games/...?privateServerLinkCode=...", px, fy, pw);
                fy += 82;
            }
            else if (m == "Follow")
            {
                Store.Cfg.LastFollow = Field(ui, "ln.follow", "Roblox username to follow", Store.Cfg.LastFollow, "the player your accounts should join", px, fy, pw);
                fy += 82;
                ui.Text("Needs the player to be in a game and allow joins.", px, fy - 8, Fonts.Small, Pal.Faint);
                fy += 20;
            }
            else
            {
                Store.Cfg.LastPlace = Field(ui, "ln.place", "Place id or game link", Store.Cfg.LastPlace, "e.g. 920587237", px, fy, pw);
                fy += 74;
                if (Store.Cfg.Recent.Count > 0)
                {
                    float rx = px;
                    foreach (string r in Store.Cfg.Recent.Take(5))
                    {
                        float w = ui.ChipWidth(r) + 6;
                        if (rx + w > px + pw) break;
                        RectangleF cr = new RectangleF(rx, fy, w, 24);
                        bool over = ui.Over(cr);
                        if (over) ui.WantHand = true;
                        ui.Chip(r, rx, fy, over ? Pal.Text : Pal.Muted);
                        if (ui.Pressed && over) ui.Active = "rc" + r;
                        if (ui.Released && ui.Active == "rc" + r) { ui.Active = null; if (over) Store.Cfg.LastPlace = r; }
                        rx += w + 4;
                    }
                    fy += 34;
                }
                if (m == "Server")
                {
                    Store.Cfg.LastJob = Field(ui, "ln.job", "Job id of the server", Store.Cfg.LastJob, "the long id of the server", px, fy, pw - 130);
                    if (ui.Button("ln.brw", new RectangleF(px + pw - 120, fy + 22, 120, 42), "Browse", Btn.Ghost, null)) { Go("Servers"); servSub = 1; pubPlace = Store.Cfg.LastPlace; }
                    fy += 80;
                }
                // search a game by name
                gameQuery = ui.TextField("ln.gq", new RectangleF(px, fy, pw - 120, 42), gameQuery, "Search a game by name");
                if (ui.Button("ln.gs", new RectangleF(px + pw - 110, fy, 110, 42), "Search", Btn.Ghost, "search")) SearchGame();
                fy += 50;
                if (gameStatus.Length > 0) { ui.Text(gameStatus, px, fy, Fonts.Small, Pal.Muted); fy += 22; }
                foreach (Roblox.GameHit h in gameHits.Take(4))
                {
                    RectangleF row = new RectangleF(px, fy, pw, 32);
                    bool over = ui.Over(row);
                    if (over) { ui.WantHand = true; ui.Fill(row, 8, Pal.Tint(18)); }
                    ui.Text(Ui.Fit(ui, h.Name, Fonts.Body, pw - 150), px + 10, fy + 6, Fonts.Body, Pal.Text);
                    ui.Text(h.Players.ToString("N0") + " playing", px + pw - 110, fy + 8, Fonts.Small, Pal.Muted);
                    if (ui.Pressed && over) ui.Active = "gh" + h.Place;
                    if (ui.Released && ui.Active == "gh" + h.Place) { ui.Active = null; if (over) { Store.Cfg.LastPlace = h.Place.ToString(); gameHits.Clear(); } }
                    fy += 34;
                }
            }
            Label(ui, "Who", px, card.Bottom - 84);
            int who = Store.Cfg.OnlySelected ? 0 : 1;
            int nw = ui.Segmented("ln.who", new RectangleF(px, card.Bottom - 62, pw, 42), new string[] { Lang.F("Selected ({0})", selected.Count), Lang.F("All ({0})", Store.Accounts.Count) }, who);
            if (nw != who) { Store.Cfg.OnlySelected = nw == 0; Store.SaveSettings(); }

            RectangleF opt = new RectangleF(a.X + lw + 16, y, a.Width - lw - 16, 540);
            ui.Glass(opt, 18, 0);
            float ox = opt.X + 28, ow = opt.Width - 56;
            ui.Text("Options", ox, opt.Y + 22, Fonts.H2, Pal.Text);
            bool own = Tog(ui, "ln.own", "Own game per account", "Accounts with their own game or server use it.", ox, opt.Y + 66, ow, Store.Cfg.UseOwn);
            if (own != Store.Cfg.UseOwn) { Store.Cfg.UseOwn = own; Store.SaveSettings(); }
            bool once = Tog(ui, "ln.once", "Start all at once", "Every client opens the same moment. Heavy for a few seconds.", ox, opt.Y + 124, ow, Store.Cfg.LaunchAtOnce);
            if (once != Store.Cfg.LaunchAtOnce) { Store.Cfg.LaunchAtOnce = once; Store.SaveSettings(); }
            ui.Text("Pause between accounts", ox, opt.Y + 184, Fonts.BodyB, once ? Pal.Faint : Pal.Text);
            ui.Text((Store.Cfg.StaggerMs / 1000.0).ToString("0.0") + " s", opt.Right - 28 - 50, opt.Y + 186, Fonts.Small, once ? Pal.Faint : Pal.A);
            int st = ui.Slider("ln.stagger", new RectangleF(ox, opt.Y + 214, ow, 24), Store.Cfg.StaggerMs, 0, 5000);
            st = (st / 50) * 50;
            if (!once && st != Store.Cfg.StaggerMs) { Store.Cfg.StaggerMs = st; Store.SaveSettings(); }
            ui.Text(once ? "Not used while everything starts at once." : "A short pause is gentler on weak PCs.", ox, opt.Y + 244, Fonts.Small, Pal.Faint);
            bool aa = Tog(ui, "ln.arr", "Arrange the windows", "Fits them on screen by themselves (see Layout).", ox, opt.Y + 288, ow, Store.Cfg.AutoArrange);
            if (aa != Store.Cfg.AutoArrange) { Store.Cfg.AutoArrange = aa; Store.SaveSettings(); }
            bool mi = Tog(ui, "ln.multi", "Multi-instance", Engine.MultiActive ? "On: several Roblox windows can run." : "Off: Roblox allows only one window.", ox, opt.Y + 346, ow, Store.Cfg.MultiInstance);
            if (mi != Store.Cfg.MultiInstance) { Store.Cfg.MultiInstance = mi; Store.SaveSettings(); Engine.SetMultiInstance(mi); }
            bool rj = Tog(ui, "ln.rejoin", "Auto-relaunch", "Bring accounts back after a crash or kick.", ox, opt.Y + 404, ow, Store.Cfg.AutoRelaunch);
            if (rj != Store.Cfg.AutoRelaunch) { Store.Cfg.AutoRelaunch = rj; Store.SaveSettings(); }
            bool rc = Tog(ui, "ln.rc", "Refresh cookie on launch", "Gets a fresh login each time (signs out other sessions).", ox, opt.Y + 462, ow, Store.Cfg.RefreshCookie);
            if (rc != Store.Cfg.RefreshCookie) { Store.Cfg.RefreshCookie = rc; Store.SaveSettings(); }
            ui.Text("Launch method", ox, opt.Y + 514 - 6, Fonts.BodyB, Pal.Text);
            int lm = ui.Segmented("ln.lm", new RectangleF(ox + ow - 190, opt.Y + 500, 190, 36), new string[] { "Protocol", "Direct" }, Store.Cfg.LaunchMethod == "Direct" ? 1 : 0);
            if ((lm == 1) != (Store.Cfg.LaunchMethod == "Direct")) { Store.Cfg.LaunchMethod = lm == 1 ? "Direct" : "Protocol"; Store.SaveSettings(); }

            float by = y + 564;
            bool busy = Engine.Busy;
            if (busy) ui.Spinner(new RectangleF(a.X + 16, by + 17, 18, 18), Pal.OnAccent);
            if (ui.Button("ln.go", new RectangleF(a.X, by, 240, 52), busy ? Engine.BusyText : "Launch", Btn.Primary, busy ? null : "play") && !busy)
            {
                Store.SaveSettings();
                List<Account> list = Store.Cfg.OnlySelected ? SelectedList() : Store.Accounts.OrderBy(x => x.Order).ToList();
                if (list.Count == 0) Log.Add(Store.Cfg.OnlySelected ? "Select accounts on the Accounts page first, or switch to All." : "There are no accounts yet.");
                else { Remember(BuildTarget().Place); LaunchList(list); }
            }
            if (ui.Button("ln.stop", new RectangleF(a.X + 252, by, 150, 52), "Stop all", Btn.Danger, "stop")) Engine.StopAll();
            if (Log.Lines.Count > 0)
            {
                Log.Entry last;
                lock (Log.Lines) last = Log.Lines[Log.Lines.Count - 1];
                ui.Text(Ui.Fit(ui, last.Text, Fonts.Body, a.Width - 430), a.X + 420, by + 16, Fonts.Body, Pal.Muted);
            }
            ui.EndScroll();
        }
    }
}
