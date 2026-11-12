using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Fleet
{
    // downloads and switches Roblox versions, for all accounts or for single accounts
    public partial class App
    {
        string verStatus = "";
        List<Versions.Release> releases = new List<Versions.Release>();

        static string Size(long bytes) { return bytes >= (1L << 30) ? (bytes / (double)(1L << 30)).ToString("0.0") + " GB" : (bytes >> 20) + " MB"; }

        void SetRoblox(Ui ui, RectangleF col)
        {
            Settings c = Store.Cfg;
            List<string> mine = Versions.Installed();
            string inst = Engine.FindRoblox();
            string installed = inst != null ? Path.GetFileName(Path.GetDirectoryName(inst)) : "not found";
            float y = col.Y;

            // ---- overview and default
            RectangleF s = Sect(ui, col, y, 250, "Roblox version");
            float x = s.X + 28, w = s.Width - 56, ry = s.Y + 62;
            ui.Text("Installed by Roblox:", x, ry, Fonts.Body, Pal.Muted);
            ui.Text(installed, x + 170, ry, Fonts.BodyB, Pal.Text); ry += 28;
            ui.Text("Used for launches:", x, ry, Fonts.Body, Pal.Muted);
            ui.Text(string.IsNullOrEmpty(c.RobloxVersion) ? "Roblox's own, kept up to date" : c.RobloxVersion, x + 170, ry, Fonts.BodyB, Pal.Text); ry += 40;
            string[] opts = new string[] { "Roblox's own (newest)" }.Concat(mine).ToArray();
            int cur = string.IsNullOrEmpty(c.RobloxVersion) ? 0 : Math.Max(0, mine.IndexOf(c.RobloxVersion) + 1);
            int nc = ui.Picker("r.use", new RectangleF(x, ry, w - 190, 42), opts, cur, "Default version");
            if (nc != cur) { c.RobloxVersion = nc == 0 ? "" : mine[nc - 1]; Save(); Log.Add(nc == 0 ? "Using Roblox's own version." : "Using " + c.RobloxVersion + " for launches."); }
            if (ui.Button("r.newest", new RectangleF(x + w - 180, ry, 180, 42), "Download newest", Btn.Primary, "refresh")) DownloadNewest();
            ry += 54;
            ui.Text("Accounts can use their own version too: Edit account > Roblox version.", x, ry, Fonts.Small, Pal.Faint);
            y = s.Bottom + 16;

            // ---- download in progress
            if (Versions.Current.Length > 0)
            {
                RectangleF p = new RectangleF(col.X, y, col.Width, 100);
                ui.Glass(p, 16, 0);
                ui.Text(Ui.Fit(ui, "Downloading " + Versions.Current, Fonts.BodyB, p.Width - 200), p.X + 24, p.Y + 18, Fonts.BodyB, Pal.Text);
                ui.Text(Ui.Fit(ui, verStatus, Fonts.Small, p.Width - 200), p.X + 24, p.Y + 42, Fonts.Small, Pal.Muted);
                ui.Bar(new RectangleF(p.X + 24, p.Y + 70, p.Width - 200, 8), ui.Anim("verprog", Versions.Progress, 0.2f), Pal.A, Pal.A);
                ui.Spinner(new RectangleF(p.Right - 160, p.Y + 20, 18, 18), Pal.Muted);
                if (ui.Button("r.cancel", new RectangleF(p.Right - 130, p.Y + 16, 106, 36), "Cancel", Btn.Danger, null)) Versions.CancelRequested = true;
                y += 116;
            }

            // ---- downloaded versions
            RectangleF d = Sect(ui, col, y, 120 + Math.Max(1, mine.Count) * 56, "Downloaded versions");
            ry = d.Y + 62;
            if (mine.Count == 0) ui.Text("None yet. Download one from the list below.", d.X + 28, ry, Fonts.Body, Pal.Muted);
            foreach (string v in mine.ToList())
            {
                int users = Store.Accounts.Count(q => q.RobloxVersion == v);
                bool isDefault = c.RobloxVersion == v;
                ui.Text(Ui.Fit(ui, v, Fonts.BodyB, 300), d.X + 28, ry + 6, Fonts.BodyB, Pal.Text);
                ui.Text(Size(Versions.SizeOf(v)), d.X + 28 + 310, ry + 8, Fonts.Small, Pal.Muted);
                float cx = d.X + 28 + 390;
                if (isDefault) { ui.Chip("Default", cx, ry + 6, Pal.Good); cx += ui.ChipWidth("Default") + 6; }
                if (users > 0) ui.Chip(Lang.F("{0} account(s)", users), cx, ry + 6, Pal.Muted);
                float bx = d.Right - 28;
                bx -= 36; if (ui.Button("r.del" + v, new RectangleF(bx, ry, 36, 34), "", Btn.Quiet, "trash")) { string err = Versions.Delete(v); Log.Add(err ?? "Deleted " + v + "."); break; }
                bx -= 100; if (!isDefault && ui.Button("r.set" + v, new RectangleF(bx, ry, 92, 34), "Use", Btn.Ghost, null)) { c.RobloxVersion = v; Save(); Log.Add("Using " + v + " for launches."); }
                ry += 56;
            }
            y = d.Bottom + 16;

            // ---- recent releases
            RectangleF r = Sect(ui, col, y, 150 + Math.Max(0, Math.Min(10, releases.Count)) * 46, "Recent releases");
            ry = r.Y + 62;
            if (ui.Button("r.list", new RectangleF(r.X + 28, ry, 200, 40), releases.Count > 0 ? "Refresh list" : "Show releases", Btn.Ghost, "refresh")) LoadReleases();
            ui.Text(Ui.Fit(ui, verStatus, Fonts.Small, r.Width - 300), r.X + 244, ry + 12, Fonts.Small, Pal.Muted);
            ry += 52;
            foreach (Versions.Release rel in releases.Take(10))
            {
                bool have = mine.Contains(rel.Version);
                ui.Text(rel.Version, r.X + 28, ry + 8, Fonts.Body, Pal.Text);
                ui.Text(rel.Date, r.X + 28 + 300, ry + 9, Fonts.Small, Pal.Muted);
                bool busy = Versions.Current.Length > 0;
                if (ui.Button("r.d" + rel.Version, new RectangleF(r.Right - 28 - 130, ry, 130, 34), have ? "Downloaded" : "Download", have || busy ? Btn.Ghost : Btn.Primary, null) && !have && !busy) DownloadVersion(rel.Version);
                ry += 46;
            }
            ui.Text("Old versions often cannot join games any more, because Roblox forces updates.", r.X + 28, r.Bottom - 34, Fonts.Small, Pal.Faint);
        }

        async void LoadReleases()
        {
            verStatus = "Loading...";
            try { releases = await Versions.Recent(); verStatus = releases.Count + " releases"; }
            catch (Exception ex) { verStatus = "Could not load: " + ex.Message; }
        }

        async void DownloadNewest()
        {
            if (releases.Count == 0) { try { verStatus = "Loading..."; releases = await Versions.Recent(); } catch (Exception ex) { verStatus = "Could not load: " + ex.Message; return; } }
            if (releases.Count > 0) DownloadVersion(releases[0].Version);
        }

        async void DownloadVersion(string v)
        {
            verStatus = "Starting...";
            string err = await Versions.Download(v, delegate(string t) { verStatus = t; });
            verStatus = err ?? "Downloaded " + v + ".";
            Log.Add(err != null ? "Version download failed: " + err : "Downloaded " + v + ".");
        }
    }
}
