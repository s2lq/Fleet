using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fleet
{
    public partial class App
    {
        int servSub;
        string pubPlace = "";
        List<Roblox.PublicServer> pubServers = new List<Roblox.PublicServer>();
        string pubStatus = "";
        int pubSort;

        void DrawServers(Ui ui, RectangleF a)
        {
            Title(ui, a, "Servers", "Saved private servers and the public servers of a game.");
            servSub = ui.Segmented("sv.sub", new RectangleF(a.Right - 360, a.Y + 6, 360, 40), new string[] { "Private servers", "Public servers" }, servSub);
            if (servSub == 0) DrawPrivate(ui, a); else DrawPublic(ui, a);
        }

        // ---------------------------------------------------------------- private
        void DrawPrivate(Ui ui, RectangleF a)
        {
            float by = a.Y + 84;
            if (ui.Button("ps.add", new RectangleF(a.X, by, 160, 42), "Add server", Btn.Primary, "plus")) AddServer();
            if (ui.Button("ps.chk", new RectangleF(a.X + 170, by, 160, 42), "Check all links", Btn.Ghost, "refresh")) CheckServers(Store.Servers.ToList());
            ui.Text(Lang.F("{0} saved, {1} account(s) selected", Store.Servers.Count, selected.Count), a.X + 346, by + 12, Fonts.Body, Pal.Muted);

            RectangleF view = new RectangleF(a.X - 8, by + 58, a.Width + 16, a.Bottom - by - 58);
            if (Store.Servers.Count == 0)
            {
                ui.TextIn("No private servers yet. Add one, then give it to accounts.", view, Fonts.Body, Pal.Muted, StringAlignment.Center);
                return;
            }
            float rh = 76;
            ui.BeginScroll(view, Store.Servers.Count * (rh + 10) + 10, Scroll("servers"));
            float y = view.Y + 4;
            foreach (SavedServer s in Store.Servers.ToList())
            {
                RectangleF r = new RectangleF(a.X, y, a.Width, rh);
                ui.Glass(r, 14, 0);
                int count = Store.Accounts.Count(q => q.ServerId == s.Id);
                ui.Text(Ui.Fit(ui, s.Name, Fonts.BodyB, 260), r.X + 22, r.Y + 14, Fonts.BodyB, Pal.Text);
                ui.Text("place " + s.Place + "   " + Lang.F("{0} account(s)", count), r.X + 22, r.Y + 40, Fonts.Small, Pal.Muted);
                Color cc = s.Check == 1 ? Pal.Good : (s.Check == 2 ? Pal.Bad : Pal.Muted);
                string ct = s.Check == 0 ? "Not checked" : s.CheckText;
                ui.Chip(Ui.Fit(ui, ct, Fonts.SmallB, 190), r.X + 330, r.Y + 26, cc);
                float bx = r.Right - 16;
                bx -= 36; if (ui.Button("ps.d" + s.Id, new RectangleF(bx, r.Y + 20, 36, 36), "", Btn.Quiet, "trash")) DeleteServer(s);
                bx -= 40; if (ui.Button("ps.c" + s.Id, new RectangleF(bx, r.Y + 20, 36, 36), "", Btn.Quiet, "refresh")) CheckServers(new List<SavedServer> { s });
                bx -= 112; if (ui.Button("ps.l" + s.Id, new RectangleF(bx, r.Y + 20, 104, 36), "Launch", Btn.Primary, "play")) LaunchServerAccounts(s);
                bx -= 100; if (ui.Button("ps.u" + s.Id, new RectangleF(bx, r.Y + 20, 92, 36), "Unassign", Btn.Ghost, null)) { foreach (Account q in SelectedList()) if (q.ServerId == s.Id) q.ServerId = ""; Store.SaveAccounts(); }
                bx -= 100; if (ui.Button("ps.a" + s.Id, new RectangleF(bx, r.Y + 20, 92, 36), "Assign", Btn.Ghost, null)) { foreach (Account q in SelectedList()) q.ServerId = s.Id; Store.SaveAccounts(); }
                y += rh + 10;
            }
            ui.EndScroll();
        }

        void AddServer()
        {
            canvas.Post(delegate
            {
                string name, link;
                if (Dialogs.AddServer(canvas.FindForm(), out name, out link) == null) return;
                string code = Roblox.ParseLinkCode(link);
                long place = Roblox.ParsePlace(link);
                if (code == null || place <= 0) { Log.Add("That is not a private server link (it needs privateServerLinkCode=...)."); return; }
                if (name.Length == 0) name = "Server " + (Store.Servers.Count + 1);
                Account owner = SelectedList().FirstOrDefault() ?? Store.Accounts.FirstOrDefault();
                SavedServer s = new SavedServer { Name = name, Link = link, Place = place, Code = code, Owner = owner != null ? owner.Id : "" };
                Store.Servers.Add(s);
                Store.SaveAccounts();
                CheckServers(new List<SavedServer> { s });
            });
        }

        void DeleteServer(SavedServer s)
        {
            canvas.Post(delegate
            {
                if (!Dialogs.Confirm(canvas.FindForm(), "Remove private server", Lang.F("Remove {0}? The real server on Roblox is not touched.", s.Name), "Remove")) return;
                foreach (Account q in Store.Accounts.Where(x => x.ServerId == s.Id)) q.ServerId = "";
                Store.Servers.Remove(s); Store.SaveAccounts();
            });
        }

        async void CheckServers(List<SavedServer> list)
        {
            if (list == null) return;
            foreach (SavedServer s in list)
            {
                Account who = Store.Find(s.Owner) ?? Store.Accounts.FirstOrDefault(q => q.Status != 2);
                if (who == null) { s.Check = 2; s.CheckText = "No account to check with"; continue; }
                try
                {
                    string r = await Roblox.CheckPrivate(who, s.Place, s.Code);
                    s.Check = r == "OK" ? 1 : 2; s.CheckText = r == "OK" ? "Joinable" : r;
                }
                catch (Exception ex) { s.Check = 2; s.CheckText = "Could not check"; Log.Add("Link check failed: " + ex.Message); }
            }
            Store.SaveAccounts();
        }

        void LaunchServerAccounts(SavedServer s)
        {
            List<Account> l = Store.Accounts.Where(q => q.ServerId == s.Id).OrderBy(q => q.Order).ToList();
            if (l.Count == 0) { Log.Add("No account has " + s.Name + " yet. Select accounts and press Assign."); return; }
            canvas.Post(delegate { Engine.LaunchMany(l, new Target { Mode = "Private", Place = s.Place, LinkCode = s.Code }); });
        }

        // ---------------------------------------------------------------- public
        void DrawPublic(Ui ui, RectangleF a)
        {
            float by = a.Y + 84;
            if (pubPlace.Length == 0) pubPlace = Store.Cfg.LastPlace;
            pubPlace = ui.TextField("pb.place", new RectangleF(a.X, by, 300, 42), pubPlace, "Place id or game link");
            if (ui.Button("pb.go", new RectangleF(a.X + 310, by, 130, 42), "Load", Btn.Primary, "refresh")) LoadPublic();
            pubSort = ui.Segmented("pb.sort", new RectangleF(a.X + 456, by, 360, 42), new string[] { "Fewest players", "Most players", "Best ping" }, pubSort);
            if (ui.Button("pb.rand", new RectangleF(a.Right - 180, by, 180, 42), "Random server", Btn.Ghost, null) && pubServers.Count > 0) JoinPublic(pubServers[new Random().Next(pubServers.Count)]);
            if (pubStatus.Length > 0) ui.Text(pubStatus, a.X, by + 52, Fonts.Small, Pal.Muted);
            ui.Text(Lang.F("Click a server to put the {0} selected account(s) in it.", selected.Count), a.X, by + 52 + (pubStatus.Length > 0 ? 20 : 0), Fonts.Small, Pal.Faint);

            RectangleF view = new RectangleF(a.X - 8, by + 96, a.Width + 16, a.Bottom - by - 96);
            IEnumerable<Roblox.PublicServer> q = pubServers;
            q = pubSort == 0 ? q.OrderBy(s => s.Playing) : (pubSort == 1 ? q.OrderByDescending(s => s.Playing) : q.OrderBy(s => s.Ping <= 0 ? 9999 : s.Ping));
            List<Roblox.PublicServer> list = q.ToList();
            if (list.Count == 0) { ui.TextIn("Load a game to see its servers.", view, Fonts.Body, Pal.Muted, StringAlignment.Center); return; }
            float rh = 46;
            ui.BeginScroll(view, list.Count * (rh + 6) + 8, Scroll("public"));
            float y = view.Y + 4;
            foreach (Roblox.PublicServer s in list)
            {
                RectangleF r = new RectangleF(a.X, y, a.Width, rh);
                bool over = ui.Over(r);
                ui.Glass(r, 12, ui.Anim("ps" + s.Id, over));
                if (over) ui.WantHand = true;
                ui.Text(s.Playing + " / " + s.Max, r.X + 22, r.Y + 13, Fonts.BodyB, Pal.Text);
                ui.Bar(new RectangleF(r.X + 120, r.Y + 19, 180, 8), s.Max > 0 ? s.Playing / (float)s.Max : 0, Pal.A, Pal.A);
                ui.Text(s.Ping > 0 ? s.Ping + " ms" : "", r.X + 340, r.Y + 14, Fonts.Body, Pal.Muted);
                ui.Text(s.Fps > 0 ? s.Fps.ToString("0") + " fps" : "", r.X + 430, r.Y + 14, Fonts.Body, Pal.Muted);
                ui.Text(Ui.Fit(ui, s.Id, Fonts.Small, 300), r.Right - 330, r.Y + 16, Fonts.Small, Pal.Faint);
                if (ui.Pressed && over) ui.Active = "pb" + s.Id;
                if (ui.Released && ui.Active == "pb" + s.Id) { ui.Active = null; if (over) JoinPublic(s); }
                y += rh + 6;
            }
            ui.EndScroll();
        }

        async void LoadPublic()
        {
            long place = Roblox.ParsePlace(pubPlace);
            if (place <= 0) { pubStatus = "Type a place id or paste a game link."; return; }
            pubStatus = "Loading...";
            try { pubServers = await Roblox.Servers(place); pubStatus = pubServers.Count + " servers"; Store.Cfg.LastPlace = place.ToString(); }
            catch (Exception ex) { pubStatus = "Could not load: " + ex.Message; }
        }

        void JoinPublic(Roblox.PublicServer s)
        {
            long place = Roblox.ParsePlace(pubPlace);
            List<Account> l = SelectedList();
            if (l.Count == 0) { Log.Add("Select accounts on the Accounts page first."); return; }
            canvas.Post(delegate { Engine.LaunchMany(l, new Target { Mode = "Server", Place = place, Job = s.Id }); });
        }
    }
}
