using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fleet
{
    public partial class App
    {
        string search = "";
        int groupIdx, tagIdx, viewIdx;
        CancellationTokenSource loginCts;
        string loginStatus = "";

        static readonly string[] ColorNames = { "Red", "Orange", "Yellow", "Green", "Teal", "Blue", "Purple", "Pink" };
        static readonly Color[] ColorVals = { Color.FromArgb(239, 83, 80), Color.FromArgb(255, 152, 67), Color.FromArgb(250, 204, 21), Color.FromArgb(74, 210, 130), Color.FromArgb(45, 212, 191), Color.FromArgb(90, 150, 255), Color.FromArgb(167, 139, 250), Color.FromArgb(244, 114, 182) };

        static Color ColorOf(string name) { int i = Array.IndexOf(ColorNames, name); return i < 0 ? Color.Empty : ColorVals[i]; }

        List<Account> Visible(List<string> groups, List<string> tags)
        {
            IEnumerable<Account> q = Store.Accounts.OrderBy(a => a.Order);
            if (groupIdx > 0 && groupIdx <= groups.Count) { string g = groups[groupIdx - 1]; q = q.Where(a => a.Group == g); }
            if (tagIdx > 0 && tagIdx <= tags.Count) { string t = tags[tagIdx - 1]; q = q.Where(a => a.TagList().Contains(t, StringComparer.OrdinalIgnoreCase)); }
            if (search.Trim().Length > 0)
            {
                string s = search.Trim().ToLower();
                q = q.Where(a => (a.Name ?? "").ToLower().Contains(s) || (a.Display ?? "").ToLower().Contains(s) || (a.Alias ?? "").ToLower().Contains(s) || (a.Note ?? "").ToLower().Contains(s));
            }
            return q.ToList();
        }

        void DrawAccounts(Ui ui, RectangleF a)
        {
            Title(ui, a, "Accounts", Lang.F("{0} saved, {1} selected", Store.Accounts.Count, selected.Count));
            List<string> groups = Store.Groups(), tags = Store.AllTags();
            if (groupIdx > groups.Count) groupIdx = 0;
            if (tagIdx > tags.Count) tagIdx = 0;

            float ty = a.Y + 86;
            float x = a.X;
            // on narrow windows the search box and the pickers give up some width so nothing runs into the buttons on the right
            float over = Math.Max(0, 240 + 10 + 160 + 150 + 150 - (a.Width - 336));
            float sbw = Math.Max(130, 240 - over); over -= 240 - sbw;
            float gw = Math.Max(104, 150 - over / 2), tw = Math.Max(96, 140 - over / 2);
            RectangleF sb = new RectangleF(x, ty, sbw, 42);
            search = ui.TextField("acc.search", sb, search, sbw < 200 ? "Search" : "Search accounts");
            ui.Icon("search", new RectangleF(sb.Right - 34, ty + 12, 18, 18), Pal.Faint, 1.6f);
            x = sb.Right + 10;
            string[] gi = new string[] { "All groups" }.Concat(groups).ToArray();
            groupIdx = ui.Picker("acc.group", new RectangleF(x, ty, gw, 42), gi, groupIdx, "Group"); x += gw + 10;
            string[] ti = new string[] { "All tags" }.Concat(tags).ToArray();
            tagIdx = ui.Picker("acc.tag", new RectangleF(x, ty, tw, 42), ti, tagIdx, "Tag"); x += tw + 10;
            viewIdx = ui.Segmented("acc.view", new RectangleF(x, ty, 150, 42), new string[] { "Cards", "List" }, viewIdx);

            float bx = a.Right;
            bx -= 150; if (ui.Button("acc.paste", new RectangleF(bx, ty, 150, 42), "Paste cookies", Btn.Ghost, "plus")) AddAccounts();
            bx -= 168; if (ui.Button("acc.browser", new RectangleF(bx, ty, 158, 42), loginCts != null ? "Cancel login" : "Browser login", Btn.Primary, loginCts != null ? "x" : "plus")) BrowserAdd();
            if (loginStatus.Length > 0) ui.Text(Ui.Fit(ui, loginStatus, Fonts.Small, 420), a.Right - 420, ty + 48, Fonts.Small, Pal.Muted);

            List<Account> list = Visible(groups, tags);
            float by = ty + 58;
            // actions on the selected accounts
            if (selected.Count > 0)
            {
                float ax = a.X;
                bool tight = a.Width < 1100;
                float[] bw = tight ? new float[] { 96, 78, 84, 80, 66, 80, 110, 130 } : new float[] { 118, 96, 104, 100, 90, 100, 128, 150 };
                float limit = a.Right - 204;
                if (ui.Button("bk.go", new RectangleF(ax, by, bw[0], 38), "Launch", Btn.Primary, "play")) LaunchList(SelectedList()); ax += bw[0] + 8;
                if (ui.Button("bk.stop", new RectangleF(ax, by, bw[1], 38), "Stop", Btn.Danger, "stop")) { foreach (Account s in SelectedList()) { Running r = Engine.Of(s); if (r != null) Engine.Kill(r); } } ax += bw[1] + 8;
                bool anyMuted = SelectedList().Any(s => s.Muted);
                if (ui.Button("bk.mute", new RectangleF(ax, by, bw[2], 38), anyMuted ? "Unmute" : "Mute", Btn.Ghost, null)) MuteSelected(!anyMuted); ax += bw[2] + 8;
                if (ui.Button("bk.check", new RectangleF(ax, by, bw[3], 38), "Check", Btn.Ghost, null)) foreach (Account s in SelectedList()) CheckOne(s); ax += bw[3] + 8;
                if (ui.Button("bk.tag", new RectangleF(ax, by, bw[4], 38), "Tag", Btn.Ghost, null)) BulkText("Tag", "Tag to add to the selected accounts:", delegate(Account s, string v) { var l = s.TagList(); if (!l.Contains(v, StringComparer.OrdinalIgnoreCase)) l.Add(v); s.Tags = string.Join(",", l.ToArray()); }); ax += bw[4] + 8;
                if (ui.Button("bk.grp", new RectangleF(ax, by, bw[5], 38), "Group", Btn.Ghost, null)) BulkText("Group", "Group for the selected accounts (empty = none):", delegate(Account s, string v) { s.Group = v; }); ax += bw[5] + 8;
                string[] cols = new string[] { "(no colour)" }.Concat(ColorNames).ToArray();
                int ci = -1;
                if (ax + bw[6] <= limit) { ci = ui.Picker("bk.col", new RectangleF(ax, by, bw[6], 38), cols, -1, "Colour"); ax += bw[6] + 8; }
                if (ci >= 0) { foreach (Account s in SelectedList()) s.Color = ci == 0 ? "" : ColorNames[ci - 1]; Store.SaveAccounts(); }
                string[] sv = new string[] { "(no server)" }.Concat(Store.Servers.Select(q => q.Name)).ToArray();
                int si = -1;
                if (ax + bw[7] <= limit) { si = ui.Picker("bk.srv", new RectangleF(ax, by, bw[7], 38), sv, -1, "Private server"); ax += bw[7] + 8; }
                if (si >= 0) { foreach (Account s in SelectedList()) s.ServerId = si == 0 ? "" : Store.Servers[si - 1].Id; Store.SaveAccounts(); }
                if (ui.Button("bk.rm", new RectangleF(a.Right - 100, by, 100, 38), "Remove", Btn.Danger, "trash")) RemoveSelected();
                if (ui.Button("bk.clr", new RectangleF(a.Right - 196, by, 88, 38), "Clear", Btn.Ghost, null)) selected.Clear();
            }
            else
            {
                bool all = list.Count > 0 && list.All(q => selected.Contains(q.Id));
                if (ui.Button("acc.sel", new RectangleF(a.X, by, 124, 38), "Select all", Btn.Ghost, "check")) foreach (Account s in list) selected.Add(s.Id);
                if (ui.Button("acc.check", new RectangleF(a.X + 132, by, 124, 38), "Check all", Btn.Ghost, "refresh")) CheckAll(false);
                if (ui.Button("acc.robux", new RectangleF(a.X + 264, by, 124, 38), "Robux", Btn.Ghost, "refresh")) RefreshRobuxAll();
            }

            RectangleF view = new RectangleF(a.X - 8, by + 52, a.Width + 16, a.Bottom - by - 52);
            if (list.Count == 0)
            {
                ui.TextIn(Store.Accounts.Count == 0 ? "No accounts yet. Use Browser login or Paste cookies." : "No account matches.", view, Fonts.Body, Pal.Muted, StringAlignment.Center);
                return;
            }
            ScrollState sc = Scroll("accounts");
            if (viewIdx == 1)
            {
                float rh = 62;
                ui.BeginScroll(view, list.Count * (rh + 8) + 8, sc);
                for (int i = 0; i < list.Count; i++)
                {
                    float e = ui.Appear("r" + list[i].Id, i);
                    if (e <= 0.001f) continue;
                    GraphicsState gs = ui.G.Save();
                    ui.G.TranslateTransform(0, (1f - e) * 16f);
                    DrawRow(ui, new RectangleF(a.X, view.Y + 4 + i * (rh + 8), a.Width, rh), list[i]);
                    ui.G.Restore(gs);
                }
                ui.EndScroll();
                return;
            }
            float cw = 292, gap = 16;
            int cols2 = Math.Max(1, (int)((a.Width + gap) / (cw + gap)));
            float realW = (a.Width - gap * (cols2 - 1)) / cols2;
            int rows = (list.Count + cols2 - 1) / cols2;
            float ch = 218;
            ui.BeginScroll(view, rows * (ch + gap) + 8, sc);
            for (int i = 0; i < list.Count; i++)
            {
                float e = ui.Appear("c" + list[i].Id, i);
                if (e <= 0.001f) continue;
                GraphicsState gs = ui.G.Save();
                ui.G.TranslateTransform(0, (1f - e) * 22f);
                DrawCard(ui, new RectangleF(a.X + (i % cols2) * (realW + gap), view.Y + 4 + (i / cols2) * (ch + gap), realW, ch), list[i]);
                ui.G.Restore(gs);
            }
            ui.EndScroll();
        }

        List<Account> SelectedList() { return Store.Accounts.Where(s => selected.Contains(s.Id)).OrderBy(s => s.Order).ToList(); }

        bool SelBox(Ui ui, RectangleF cb, Account a)
        {
            bool sel = selected.Contains(a.Id);
            bool over = ui.Over(cb);
            if (over) ui.WantHand = true;
            if (sel) { ui.Fill(cb, 11, Pal.A); ui.Icon("check", new RectangleF(cb.X + 3, cb.Y + 3, 16, 16), Pal.OnAccent, 2f); }
            else ui.Stroke(cb, 11, Pal.Tint(over ? 160 : 90), 1.4f);
            if (ui.Pressed && over) ui.Active = "sel" + a.Id;
            if (ui.Released && ui.Active == "sel" + a.Id) { ui.Active = null; if (over) { if (sel) selected.Remove(a.Id); else selected.Add(a.Id); } }
            return sel;
        }

        string StatusText(Account a, Running run)
        {
            return run != null ? "Running" : (a.Status == 1 ? "Working" : (a.Status == 2 ? "Signed out" : (a.Status == 3 ? "Locked" : "Not checked")));
        }

        Color StatusColor(Account a, Running run)
        {
            return run != null || a.Status == 1 ? Pal.Good : (a.Status == 2 || a.Status == 3 ? Pal.Bad : Pal.Muted);
        }

        void DrawCard(Ui ui, RectangleF r, Account a)
        {
            bool sel = selected.Contains(a.Id);
            float lift = ui.Anim("card." + a.Id, ui.Over(r));
            ui.Glass(r, 18, lift);
            if (sel) ui.Stroke(r, 12, Pal.Alpha(Pal.A, 200), 1.6f);
            Color col = ColorOf(a.Color);
            if (!col.IsEmpty) ui.Fill(new RectangleF(r.X, r.Y + 14, 3, r.Height - 28), 1.5f, col);
            Running run = Engine.Of(a);

            AvatarButton(ui, new RectangleF(r.X + 18, r.Y + 18, 52, 52), a);
            if (run != null) { ui.Pulse(r.X + 63, r.Y + 63, 7, Pal.Good); ui.Fill(new RectangleF(r.X + 56, r.Y + 56, 14, 14), 7, Pal.Good); }
            ui.Text(Ui.Fit(ui, a.Label, Fonts.BodyB, r.Width - 140), r.X + 82, r.Y + 22, Fonts.BodyB, Pal.Text);
            ui.Text(Ui.Fit(ui, "@" + a.Name, Fonts.Small, r.Width - 140), r.X + 82, r.Y + 44, Fonts.Small, Pal.Muted);
            SelBox(ui, new RectangleF(r.Right - 18 - 22, r.Y + 18, 22, 22), a);

            float cy = r.Y + 82;
            string st = StatusText(a, run);
            ui.Chip(st, r.X + 18, cy, StatusColor(a, run));
            float cx = r.X + 18 + ui.ChipWidth(st) + 6;
            if (a.Muted) { ui.Chip("Muted", cx, cy, Pal.Warn); cx += ui.ChipWidth("Muted") + 6; }
            SavedServer sv = Store.FindServer(a.ServerId);
            string where = sv != null ? sv.Name : (Roblox.ParseLinkCode(a.PrivateLink) != null ? "private server" : (Roblox.ParsePlace(a.Place) > 0 ? "place " + Roblox.ParsePlace(a.Place) : ""));
            if (where.Length > 0 && cx + ui.ChipWidth(where) < r.Right - 14) ui.Chip(where, cx, cy, Pal.Muted);

            float ty = cy + 28;
            float tx = r.X + 18;
            foreach (string tag in a.TagList().Take(3))
            {
                float w = ui.ChipWidth(tag);
                if (tx + w > r.Right - 14) break;
                ui.Chip(tag, tx, ty, Pal.Muted); tx += w + 6;
            }
            string line = (a.Robux >= 0 ? "R$ " + a.Robux.ToString("N0") + "   " : "") + (string.IsNullOrEmpty(a.Note) ? "" : a.Note);
            ui.Text(Ui.Fit(ui, line, Fonts.Small, r.Width - 36), r.X + 18, ty + 28, Fonts.Small, Pal.Faint);

            float by = r.Bottom - 18 - 34;
            float bw = r.Width - 36 - 4 * 40;
            if (run == null) { if (ui.Button("c.l" + a.Id, new RectangleF(r.X + 18, by, bw, 34), "Launch", Btn.Primary, "play")) LaunchList(new List<Account> { a }); }
            else if (ui.Button("c.s" + a.Id, new RectangleF(r.X + 18, by, bw, 34), "Stop", Btn.Danger, "stop")) Engine.Kill(run);
            float x = r.X + 18 + bw + 4;
            if (ui.Button("c.i" + a.Id, new RectangleF(x, by, 34, 34), "", Btn.Quiet, "info")) OpenDetails(a);
            x += 40;
            if (ui.Button("c.e" + a.Id, new RectangleF(x, by, 34, 34), "", Btn.Quiet, "edit")) EditOne(a);
            x += 40;
            if (ui.Button("c.m" + a.Id, new RectangleF(x, by, 34, 34), "", Btn.Quiet, a.Muted ? "stop" : "bolt")) { a.Muted = !a.Muted; if (run != null) Audio.SetMute(run.Pid, a.Muted); Store.SaveAccounts(); }
            x += 40;
            if (ui.Button("c.d" + a.Id, new RectangleF(x, by, 34, 34), "", Btn.Quiet, "trash")) RemoveOne(a);
        }

        void DrawRow(Ui ui, RectangleF r, Account a)
        {
            float lift = ui.Anim("row." + a.Id, ui.Over(r));
            ui.Glass(r, 14, lift);
            Running run = Engine.Of(a);
            Color col = ColorOf(a.Color);
            if (!col.IsEmpty) ui.Fill(new RectangleF(r.X, r.Y + 10, 3, r.Height - 20), 1.5f, col);
            SelBox(ui, new RectangleF(r.X + 16, r.Y + 20, 22, 22), a);
            AvatarButton(ui, new RectangleF(r.X + 52, r.Y + 11, 40, 40), a);
            ui.Text(Ui.Fit(ui, a.Label, Fonts.BodyB, 200), r.X + 104, r.Y + 10, Fonts.BodyB, Pal.Text);
            ui.Text(Ui.Fit(ui, "@" + a.Name, Fonts.Small, 200), r.X + 104, r.Y + 33, Fonts.Small, Pal.Muted);
            float x = r.X + 330;
            string st = StatusText(a, run);
            ui.Chip(st, x, r.Y + 20, StatusColor(a, run)); x += ui.ChipWidth(st) + 8;
            if (!string.IsNullOrEmpty(a.Group)) { ui.Chip(a.Group, x, r.Y + 20, Pal.Muted); x += ui.ChipWidth(a.Group) + 6; }
            foreach (string tag in a.TagList().Take(2)) { ui.Chip(tag, x, r.Y + 20, Pal.Muted); x += ui.ChipWidth(tag) + 6; }
            if (a.Robux >= 0 && r.Width > 900) ui.Text("R$ " + a.Robux.ToString("N0"), r.Right - 420, r.Y + 22, Fonts.Body, Pal.Muted);
            float bx = r.Right - 16;
            bx -= 34; if (ui.Button("r.d" + a.Id, new RectangleF(bx, r.Y + 14, 34, 34), "", Btn.Quiet, "trash")) RemoveOne(a);
            bx -= 38; if (ui.Button("r.i" + a.Id, new RectangleF(bx, r.Y + 14, 34, 34), "", Btn.Quiet, "info")) OpenDetails(a);
            bx -= 38; if (ui.Button("r.e" + a.Id, new RectangleF(bx, r.Y + 14, 34, 34), "", Btn.Quiet, "edit")) EditOne(a);
            bx -= 110;
            if (run == null) { if (ui.Button("r.l" + a.Id, new RectangleF(bx, r.Y + 14, 104, 34), "Launch", Btn.Primary, "play")) LaunchList(new List<Account> { a }); }
            else if (ui.Button("r.s" + a.Id, new RectangleF(bx, r.Y + 14, 104, 34), "Stop", Btn.Danger, "stop")) Engine.Kill(run);
        }

        // ------------------------------------------------------------ actions
        void EditOne(Account a) { canvas.Post(delegate { if (Dialogs.EditAccount(canvas.FindForm(), a)) { Store.SaveAccounts(); } }); }

        void RemoveOne(Account a)
        {
            canvas.Post(delegate
            {
                if (!Dialogs.Confirm(canvas.FindForm(), "Remove account", Lang.F("Remove {0} from Fleet? The account on Roblox is not touched.", a.Label), "Remove")) return;
                foreach (Running q in Engine.Live.Where(x => x.Acc == a).ToList()) Engine.Kill(q);
                Store.Accounts.Remove(a); selected.Remove(a.Id); Store.SaveAccounts();
            });
        }

        void RemoveSelected()
        {
            List<Account> l = SelectedList();
            canvas.Post(delegate
            {
                if (!Dialogs.Confirm(canvas.FindForm(), "Remove accounts", Lang.F("Remove {0} accounts from Fleet?", l.Count), "Remove")) return;
                foreach (Account a in l) { foreach (Running q in Engine.Live.Where(x => x.Acc == a).ToList()) Engine.Kill(q); Store.Accounts.Remove(a); }
                selected.Clear(); Store.SaveAccounts();
            });
        }

        void BulkText(string title, string label, Action<Account, string> apply)
        {
            List<Account> l = SelectedList();
            canvas.Post(delegate
            {
                string v = Dialogs.Prompt(canvas.FindForm(), Lang.T(title), Lang.T(label), "");
                if (v == null) return;
                v = v.Trim();
                foreach (Account s in l) apply(s, v);
                Store.SaveAccounts();
            });
        }

        void MuteSelected(bool mute)
        {
            foreach (Account s in SelectedList()) { s.Muted = mute; Running r = Engine.Of(s); if (r != null) Audio.SetMute(r.Pid, mute); }
            Store.SaveAccounts();
        }

        void AddAccounts()
        {
            canvas.Post(delegate
            {
                string text = Dialogs.Paste(canvas.FindForm(), Lang.T("Add accounts"), Lang.T("Paste one cookie per line."), Lang.T("A cookie starts with _|WARNING:-DO-NOT-SHARE-THIS. Lines like user:pass:cookie work too."));
                if (string.IsNullOrWhiteSpace(text)) return;
                List<string> cookies = new List<string>();
                foreach (string line in text.Split('\n'))
                {
                    string c = Roblox.ExtractCookie(line);
                    if (c != null && !cookies.Contains(c)) cookies.Add(c);
                }
                if (cookies.Count == 0) { Log.Add("No cookie found in what you pasted."); return; }
                AddCookies(cookies);
            });
        }

        async void BrowserAdd()
        {
            if (loginCts != null) { loginCts.Cancel(); return; }
            loginCts = new CancellationTokenSource();
            string c = await BrowserLogin.GetCookie(loginCts.Token, delegate(string s) { loginStatus = s; });
            loginCts = null;
            if (c != null) { loginStatus = ""; AddCookies(new List<string> { c }); } else if (loginStatus.StartsWith("Log in")) loginStatus = "";
        }

        public Task AddCookies(List<string> cookies) { return AddImported(cookies.Select(c => new Imp { Cookie = c }).ToList()); }

        public async Task AddImported(List<Imp> items)
        {
            int ok = 0, bad = 0;
            foreach (Imp it in items)
            {
                try
                {
                    Who w = await Roblox.WhoAmI(it.Cookie);
                    if (w == null || w.Id < 0) { bad++; Log.Add((it.User.Length > 0 ? it.User + ": " : "") + "the cookie did not work (signed out or locked)."); continue; }
                    Account existing = Store.Accounts.FirstOrDefault(x => x.UserId == w.Id);
                    Account a = existing;
                    if (a == null) { a = new Account { Name = w.Name, Display = w.Display, UserId = w.Id, Order = Store.Accounts.Count }; Proxies.AssignNew(a); Store.Accounts.Add(a); }
                    a.Cookie = it.Cookie; a.Status = 1; a.LastCheck = DateTime.UtcNow.Ticks;
                    if (it.Pass.Length > 0 && a.Password.Length == 0) a.Password = it.Pass;
                    if (it.Alias.Length > 0 && a.Alias.Length == 0) a.Alias = it.Alias;
                    if (it.Group.Length > 0 && it.Group != "Default" && a.Group.Length == 0) a.Group = it.Group;
                    if (it.Note.Length > 0 && a.Note.Length == 0) a.Note = it.Note;
                    ok++;
                    Log.Add((existing != null ? "Updated " : "Added ") + a.Label + ".");
                    if (existing == null) RefreshRobux(a);
                }
                catch (Exception ex) { bad++; Log.Add("Could not reach Roblox: " + ex.Message); }
            }
            Store.SaveAccounts();
            Log.Add(ok + " added" + (bad > 0 ? ", " + bad + " failed" : "") + ".");
        }

        public void ImportFromManager()
        {
            canvas.Post(delegate
            {
                using (OpenFileDialog d = new OpenFileDialog { Title = "Import accounts from another manager", Filter = "Account lists|*.json;*.txt;*.csv;*.list|All files|*.*" })
                {
                    if (d.ShowDialog(canvas.FindForm()) != DialogResult.OK) return;
                    try
                    {
                        List<Imp> list = Importer.FromFile(d.FileName);
                        if (list.Count == 0) { Log.Add("No Roblox cookies were found in that file."); return; }
                        Log.Add("Found " + list.Count + " account(s) in " + Path.GetFileName(d.FileName) + ", checking them...");
                        AddImported(list);
                    }
                    catch (Exception ex) { Log.Add("Could not read the file: " + ex.Message); }
                }
            });
        }

        public async Task RefreshRobux(Account a)
        {
            try { long r = await Roblox.Robux(a); if (r >= 0) { a.Robux = r; Store.SaveAccounts(); } } catch (Exception) { }
        }

        async void RefreshRobuxAll()
        {
            foreach (Account a in Store.Accounts.Where(x => x.UserId > 0 && x.Status != 2).ToList()) { RefreshRobux(a); await Task.Delay(300); }
        }
    }

}
