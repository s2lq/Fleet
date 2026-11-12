using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Fleet
{
    // handy bulk jobs on the saved accounts
    public partial class App
    {
        int sortKey, exportFmt, selGroup = -1;
        bool sortDesc;
        string renamePattern = "Alt {n}", bulkPlace = "", bulkNote = "", bulkFps = "", bulkProxy = "", dupStatus = "";
        static readonly string[] SortNames = { "Name", "Robux", "Last launch", "Status", "Group", "Account age" };
        static readonly string[] ExportNames = { "Cookies only", "user:pass:cookie", "Summary (no cookies)" };

        RectangleF UCard(Ui ui, float x, float y, float w, float h, string title, string sub)
        {
            RectangleF r = new RectangleF(x, y, w, h);
            ui.Glass(r, 16, 0);
            ui.Text(title, x + 24, y + 18, Fonts.H2, Pal.Text);
            if (!string.IsNullOrEmpty(sub)) ui.Text(Ui.Fit(ui, Lang.T(sub), Fonts.Small, w - 48), x + 24, y + 46, Fonts.Small, Pal.Muted);
            return r;
        }

        void DrawAccountTools(Ui ui, RectangleF a)
        {
            RectangleF view = new RectangleF(a.X - 8, a.Y + 82, a.Width + 16, a.Height - 82);
            Scroll("acctools").Auto = true;
            ui.BeginScroll(view, 1400, Scroll("acctools"));
            float gap = 16, w = (a.Width - gap) / 2, y = view.Y + 8;
            List<Account> all = Store.Accounts.OrderBy(q => q.Order).ToList();

            // ---- summary
            RectangleF s = UCard(ui, a.X, y, a.Width, 120, "Summary", null);
            long robux = all.Where(q => q.Robux > 0).Sum(q => q.Robux);
            int prem = all.Count(q => q.Premium), bad = all.Count(q => q.Status == 2 || q.Status == 3), own = all.Count(q => !string.IsNullOrEmpty(q.Place) || !string.IsNullOrEmpty(q.PrivateLink) || !string.IsNullOrEmpty(q.ServerId));
            DateTime oldest = all.Select(q => { DateTime d; return DateTime.TryParse(q.Created, null, System.Globalization.DateTimeStyles.RoundtripKind, out d) ? d : DateTime.MaxValue; }).DefaultIfEmpty(DateTime.MaxValue).Min();
            string[,] stats = { { "Accounts", all.Count.ToString() }, { "Total Robux", robux.ToString("N0") }, { "Premium", prem.ToString() }, { "Signed out or locked", bad.ToString() }, { "With their own game", own.ToString() }, { "Oldest account", oldest == DateTime.MaxValue ? "..." : oldest.Year.ToString() } };
            float cw = (a.Width - 48) / 6;
            for (int i = 0; i < 6; i++)
            {
                ui.Text(Ui.Fit(ui, Lang.T(stats[i, 0]).ToUpper(), Fonts.SmallB, cw - 12), s.X + 24 + i * cw, s.Y + 56, Fonts.SmallB, Pal.Muted);
                ui.Text(stats[i, 1], s.X + 24 + i * cw, s.Y + 76, Fonts.Get(22, true), Pal.Text);
            }
            y += 136;

            // ---- selection helpers
            RectangleF c = UCard(ui, a.X, y, w, 232, "Select", "Quick ways to pick accounts for the other tools.");
            float bx = c.X + 24, by = c.Y + 80;
            string[] names = { "All", "None", "Invert", "Working", "Signed out", "Running" };
            for (int i = 0; i < names.Length; i++)
            {
                float bw = ui.Measure(names[i], Fonts.BodyB).Width + 36;
                if (bx + bw > c.Right - 24) { bx = c.X + 24; by += 48; }
                if (ui.Button("sel." + i, new RectangleF(bx, by, bw, 38), names[i], Btn.Ghost, null)) SelectBy(i, all);
                bx += bw + 8;
            }
            List<string> groups = Store.Groups();
            if (groups.Count > 0)
            {
                int gi = ui.Picker("sel.grp", new RectangleF(c.X + 24, c.Bottom - 62, 240, 40), groups.ToArray(), -1, "Select a group");
                if (gi >= 0) { selected.Clear(); foreach (Account q in all.Where(q => q.Group == groups[gi])) selected.Add(q.Id); }
            }
            ui.Text(Lang.F("{0} selected", selected.Count), c.Right - 24 - 120, c.Bottom - 50, Fonts.Body, Pal.Muted);

            // ---- sort
            RectangleF so = UCard(ui, a.X + w + gap, y, w, 232, "Sort and order", "Changes the order of the account list.");
            sortKey = ui.Picker("so.key", new RectangleF(so.X + 24, so.Y + 80, 250, 42), SortNames, sortKey, "Sort by");
            sortDesc = Tog(ui, "so.desc", "Descending", "", so.X + 24, so.Y + 138, w - 48, sortDesc);
            if (ui.Button("so.go", new RectangleF(so.X + 24, so.Y + 176, 140, 40), "Sort now", Btn.Primary, null)) SortAccounts();
            if (selected.Count > 0 && ui.Button("so.top", new RectangleF(so.X + 174, so.Y + 176, 190, 40), "Move selected to top", Btn.Ghost, null)) MoveSelectedTop();
            y += 248;

            // ---- rename
            RectangleF rn = UCard(ui, a.X, y, w, 210, "Rename in bulk", "Gives the selected accounts an alias. {n} is a number.");
            renamePattern = ui.TextField("rn.pat", new RectangleF(rn.X + 24, rn.Y + 80, w - 48, 42), renamePattern, "Alt {n}");
            if (ui.Button("rn.go", new RectangleF(rn.X + 24, rn.Y + 142, 190, 40), "Apply to selected", Btn.Primary, null)) BulkRename();
            if (ui.Button("rn.clr", new RectangleF(rn.X + 224, rn.Y + 142, 160, 40), "Clear aliases", Btn.Ghost, null)) { foreach (Account q in SelectedList()) q.Alias = ""; Store.SaveAccounts(); }

            // ---- duplicates
            RectangleF du = UCard(ui, a.X + w + gap, y, w, 210, "Duplicates", "The same Roblox account saved twice.");
            if (ui.Button("du.find", new RectangleF(du.X + 24, du.Y + 80, 150, 40), "Find", Btn.Ghost, null)) FindDuplicates(false);
            if (ui.Button("du.rm", new RectangleF(du.X + 184, du.Y + 80, 200, 40), "Remove duplicates", Btn.Danger, "trash")) FindDuplicates(true);
            ui.Text(dupStatus, du.X + 24, du.Y + 140, Fonts.Body, Pal.Muted);
            y += 226;

            // ---- bulk edit
            RectangleF be = UCard(ui, a.X, y, w, 330, "Edit selected in bulk", "Only the fields you fill in are changed.");
            float fx = be.X + 24, fw = w - 48;
            bulkPlace = ui.TextField("be.place", new RectangleF(fx, be.Y + 76, fw, 40), bulkPlace, "Game (place id or link)");
            bulkNote = ui.TextField("be.note", new RectangleF(fx, be.Y + 124, fw, 40), bulkNote, "Note");
            bulkFps = ui.TextField("be.fps", new RectangleF(fx, be.Y + 172, (fw - 10) / 2, 40), bulkFps, "FPS cap");
            bulkProxy = ui.TextField("be.px", new RectangleF(fx + (fw + 10) / 2, be.Y + 172, (fw - 10) / 2, 40), bulkProxy, "Proxy");
            if (ui.Button("be.go", new RectangleF(fx, be.Y + 228, 170, 42), "Apply to selected", Btn.Primary, null)) BulkEdit();
            if (ui.Button("be.clear", new RectangleF(fx + 180, be.Y + 228, 190, 42), "Clear game and server", Btn.Ghost, null)) { foreach (Account q in SelectedList()) { q.Place = ""; q.PrivateLink = ""; q.ServerId = ""; } Store.SaveAccounts(); Log.Add("Games cleared on the selected accounts."); }
            ui.Text(Lang.F("{0} selected", selected.Count), fx, be.Bottom - 34, Fonts.Small, Pal.Faint);

            // ---- export
            RectangleF ex = UCard(ui, a.X + w + gap, y, w, 330, "Export selected", "Copies or saves the selected accounts as text.");
            exportFmt = ui.Picker("ex.fmt", new RectangleF(ex.X + 24, ex.Y + 80, w - 48, 42), ExportNames, exportFmt, "Format");
            if (ui.Button("ex.copy", new RectangleF(ex.X + 24, ex.Y + 140, 160, 42), "Copy", Btn.Ghost, null)) ExportSelected(false);
            if (ui.Button("ex.save", new RectangleF(ex.X + 194, ex.Y + 140, 190, 42), "Save to a file", Btn.Ghost, null)) ExportSelected(true);
            ui.Para("Cookies and passwords in these formats are readable by anyone who gets the text. Prefer Settings > Data > Export all accounts, which is password protected.", new RectangleF(ex.X + 24, ex.Y + 204, w - 48, 90), Fonts.Small, Pal.Muted);
            ui.EndScroll();
        }

        void SelectBy(int mode, List<Account> all)
        {
            switch (mode)
            {
                case 0: foreach (Account q in all) selected.Add(q.Id); break;
                case 1: selected.Clear(); break;
                case 2: foreach (Account q in all) { if (!selected.Remove(q.Id)) selected.Add(q.Id); } break;
                case 3: selected.Clear(); foreach (Account q in all.Where(q => q.Status == 1)) selected.Add(q.Id); break;
                case 4: selected.Clear(); foreach (Account q in all.Where(q => q.Status == 2 || q.Status == 3)) selected.Add(q.Id); break;
                default: selected.Clear(); foreach (Account q in all.Where(q => Engine.Of(q) != null)) selected.Add(q.Id); break;
            }
        }

        void SortAccounts()
        {
            Func<Account, object> key;
            switch (sortKey)
            {
                case 1: key = q => q.Robux; break;
                case 2: key = q => q.LastLaunch; break;
                case 3: key = q => q.Status; break;
                case 4: key = q => (q.Group ?? "").ToLower(); break;
                case 5: key = q => { DateTime d; return DateTime.TryParse(q.Created, null, System.Globalization.DateTimeStyles.RoundtripKind, out d) ? d : DateTime.MaxValue; }; break;
                default: key = q => q.Label.ToLower(); break;
            }
            List<Account> sorted = sortDesc ? Store.Accounts.OrderByDescending(key).ToList() : Store.Accounts.OrderBy(key).ToList();
            for (int i = 0; i < sorted.Count; i++) sorted[i].Order = i;
            Store.SaveAccounts();
            Log.Add("Accounts sorted by " + SortNames[sortKey].ToLower() + ".");
        }

        void MoveSelectedTop()
        {
            List<Account> top = SelectedList();
            List<Account> rest = Store.Accounts.Where(q => !selected.Contains(q.Id)).OrderBy(q => q.Order).ToList();
            int i = 0;
            foreach (Account q in top.Concat(rest)) q.Order = i++;
            Store.SaveAccounts();
        }

        void BulkRename()
        {
            List<Account> l = SelectedList();
            if (l.Count == 0) { Log.Add("Select accounts first."); return; }
            string pat = renamePattern.Contains("{n}") ? renamePattern : renamePattern + " {n}";
            for (int i = 0; i < l.Count; i++) l[i].Alias = pat.Replace("{n}", (i + 1).ToString());
            Store.SaveAccounts();
            Log.Add("Renamed " + l.Count + " account(s).");
        }

        void FindDuplicates(bool remove)
        {
            List<Account> dupes = new List<Account>();
            HashSet<long> seenIds = new HashSet<long>();
            HashSet<string> seenCookies = new HashSet<string>();
            foreach (Account q in Store.Accounts.OrderBy(q => q.Order))
            {
                bool dup = (q.UserId > 0 && !seenIds.Add(q.UserId)) | (!string.IsNullOrEmpty(q.Cookie) && !seenCookies.Add(q.Cookie));
                if (dup) dupes.Add(q);
            }
            if (!remove) { dupStatus = dupes.Count == 0 ? "No duplicates." : Lang.F("{0} duplicate(s) found.", dupes.Count); return; }
            if (dupes.Count == 0) { dupStatus = "No duplicates."; return; }
            foreach (Account q in dupes) { foreach (Running r in Engine.Live.Where(x => x.Acc == q).ToList()) Engine.Kill(r); Store.Accounts.Remove(q); selected.Remove(q.Id); }
            Store.SaveAccounts();
            dupStatus = Lang.F("{0} removed.", dupes.Count);
        }

        void BulkEdit()
        {
            List<Account> l = SelectedList();
            if (l.Count == 0) { Log.Add("Select accounts first."); return; }
            int fps; bool hasFps = int.TryParse(bulkFps, out fps);
            foreach (Account q in l)
            {
                if (bulkPlace.Trim().Length > 0) q.Place = bulkPlace.Trim();
                if (bulkNote.Trim().Length > 0) q.Note = bulkNote.Trim();
                if (hasFps) q.FpsCap = Math.Max(0, Math.Min(1000, fps));
                if (bulkProxy.Trim().Length > 0) { q.Proxy = bulkProxy.Trim(); q.ProxyAuto = false; }
            }
            Store.SaveAccounts();
            Log.Add("Updated " + l.Count + " account(s).");
        }

        string BuildExport(List<Account> l)
        {
            StringBuilder sb = new StringBuilder();
            if (exportFmt == 2) sb.AppendLine("username,display,user id,status,robux,premium,group,tags,created");
            foreach (Account q in l)
            {
                if (exportFmt == 0) sb.AppendLine(q.Cookie);
                else if (exportFmt == 1) sb.AppendLine(q.Name + ":" + q.Password + ":" + q.Cookie);
                else sb.AppendLine(string.Join(",", new string[] { q.Name, q.Display, q.UserId.ToString(), StatusText(q, null), q.Robux.ToString(), q.Premium.ToString(), q.Group, "\"" + q.Tags + "\"", q.Created }));
            }
            return sb.ToString();
        }

        void ExportSelected(bool toFile)
        {
            List<Account> l = SelectedList();
            if (l.Count == 0) { Log.Add("Select accounts first."); return; }
            canvas.Post(delegate
            {
                if (exportFmt != 2 && !Dialogs.Confirm(canvas.FindForm(), "Export logins", Lang.T("This text contains login cookies. Anyone who gets it can use these accounts. Continue?"), "Continue")) return;
                string text = BuildExport(l);
                if (!toFile) { try { Clipboard.SetText(text); Log.Add(l.Count + " account(s) copied."); } catch (Exception ex) { Log.Add("Could not copy: " + ex.Message); } return; }
                using (SaveFileDialog d = new SaveFileDialog { Filter = exportFmt == 2 ? "CSV|*.csv" : "Text|*.txt", FileName = exportFmt == 2 ? "accounts-summary.csv" : "accounts.txt" })
                    if (d.ShowDialog(canvas.FindForm()) == DialogResult.OK) { try { File.WriteAllText(d.FileName, text, Encoding.UTF8); Log.Add("Saved " + l.Count + " account(s)."); } catch (Exception ex) { Log.Add("Could not save: " + ex.Message); } }
            });
        }
    }
}
