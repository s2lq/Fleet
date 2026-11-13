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
        async void CheckOne(Account a) { await CheckAsync(a, false); }

        public async Task CheckAsync(Account a, bool quiet)
        {
            try
            {
                int before = a.Status;
                Who w = await Roblox.WhoAmI(a.Cookie);
                a.Status = w == null ? 2 : (w.Id < 0 ? 3 : 1);
                a.LastCheck = DateTime.UtcNow.Ticks;
                if (w != null && w.Id > 0) { a.Name = w.Name; a.Display = w.Display; }
                Store.SaveAccounts();
                if (a.Status == 2 && before != 2) { Journal2.Add(a, "SignedOut", "The saved login no longer works."); Shell.Toast("Signed out", a.Label + " needs a new login."); }
                if (a.Status == 3 && before != 3) { Journal2.Add(a, "Locked", "Roblox answered 403 (locked or blocked)."); if (Store.Cfg.HookLocked) Hook.Send("Account locked", a.Label + " looks locked.", Hook.Red); }
                if (!quiet) Log.Add(a.Label + ": " + (a.Status == 1 ? "login works." : (a.Status == 2 ? "signed out." : "locked or blocked.")));
            }
            catch (Exception ex) { if (!quiet) Log.Add(a.Label + ": could not check (" + ex.Message + ")."); }
        }

        async void CheckAll(bool quiet)
        {
            if (!quiet) Log.Add("Checking " + Store.Accounts.Count + " login(s)...");
            foreach (Account a in Store.Accounts.ToList()) { await CheckAsync(a, quiet); await Task.Delay(250); }
        }

        async void HealthLater() { await Task.Delay(4000); CheckAll(true); }

        static string Ago(long ticks)
        {
            if (ticks <= 0) return "never";
            TimeSpan t = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
            if (t.TotalMinutes < 1) return "just now";
            if (t.TotalHours < 1) return (int)t.TotalMinutes + " min ago";
            if (t.TotalDays < 1) return (int)t.TotalHours + " h ago";
            return (int)t.TotalDays + " d ago";
        }

        async void RefreshCookies(List<Account> list)
        {
            foreach (Account a in list)
            {
                string c = await Roblox.Rotate(a.Cookie);
                if (c != null) { a.Cookie = c; a.Status = 1; Store.SaveAccounts(); Log.Add(a.Label + ": cookie refreshed."); Journal2.Add(a, "Recovered", "Cookie refreshed."); }
                else Log.Add(a.Label + ": Roblox would not refresh the cookie. Use Browser login.");
            }
        }

        void DrawHealth(Ui ui, RectangleF a)
        {
            Title(ui, a, "Health", "See which logins work and fix the ones that stopped.");
            int ok = Store.Accounts.Count(x => x.Status == 1), out2 = Store.Accounts.Count(x => x.Status == 2), lk = Store.Accounts.Count(x => x.Status == 3), un = Store.Accounts.Count(x => x.Status == 0);
            ui.Text(Lang.F("{0} working, {1} signed out, {2} locked, {3} not checked", ok, out2, lk, un), a.X + 1, a.Y + 66, Fonts.BodyB, out2 + lk > 0 ? Pal.Warn : Pal.Good);

            float by = a.Y + 98;
            float hx = a.X;
            if (ui.Button("h.check", new RectangleF(hx, by, 150, 40), "Check all now", Btn.Primary, "refresh")) CheckAll(false); hx += 158;
            if (ui.Button("h.login", new RectangleF(hx, by, 150, 40), "Browser login", Btn.Ghost, null)) BrowserAdd(); hx += 158;
            if (ui.Button("h.cookie", new RectangleF(hx, by, 160, 40), "Refresh cookies", Btn.Ghost, null)) RefreshCookies(Store.Accounts.Where(q => q.Status == 2).ToList()); hx += 168;
            if (ui.Button("h.csv", new RectangleF(hx, by, 150, 40), "Export events", Btn.Ghost, null)) canvas.Post(ExportEvents);
            if (ui.Button("h.rm", new RectangleF(a.Right - 230, by, 230, 40), "Remove broken accounts", Btn.Danger, "trash")) RemoveBroken();

            float ay = by + 54;
            bool auto = Tog(ui, "h.auto", "Check logins automatically", Lang.F("Every {0} hours, quietly in the background.", Store.Cfg.HealthHours), a.X, ay, 420, Store.Cfg.HealthAuto);
            if (auto != Store.Cfg.HealthAuto) { Store.Cfg.HealthAuto = auto; Store.SaveSettings(); }
            int hh = ui.Slider("h.hours", new RectangleF(a.X + 460, ay + 4, 240, 24), Store.Cfg.HealthHours, 1, 24);
            if (hh != Store.Cfg.HealthHours) { Store.Cfg.HealthHours = hh; Store.SaveSettings(); }

            RectangleF view = new RectangleF(a.X - 8, ay + 50, a.Width + 16, a.Bottom - ay - 50);
            List<Account> list = Store.Accounts.OrderBy(q => q.Order).ToList();
            List<JournalEntry> ev = Enumerable.Reverse(Store.Journal).Take(30).ToList();
            float rowH = 34;
            float content = 44 + list.Count * rowH + 70 + ev.Count * 30 + 30;
            ui.BeginScroll(view, content, Scroll("health"));
            float y = view.Y + 4;
            RectangleF card = new RectangleF(a.X, y, a.Width, 40 + list.Count * rowH + 10);
            ui.Glass(card, 16, 0);
            float[] cx = { 24, 270, 400, 520, 640, 760 };
            string[] heads = { "Account", "Status", "Checked", "Last launch", "Crashes 7d", "What to do" };
            for (int i = 0; i < heads.Length; i++) ui.Text(heads[i], card.X + cx[i], card.Y + 14, Fonts.SmallB, Pal.Muted);
            float ry = card.Y + 40;
            foreach (Account q in list)
            {
                Running run = Engine.Of(q);
                ui.Text(Ui.Fit(ui, q.Label, Fonts.Body, 230), card.X + cx[0], ry + 6, Fonts.Body, Pal.Text);
                ui.Text(StatusText(q, run), card.X + cx[1], ry + 6, Fonts.Body, StatusColor(q, run));
                ui.Text(Ago(q.LastCheck), card.X + cx[2], ry + 6, Fonts.Body, Pal.Muted);
                ui.Text(Ago(q.LastLaunch), card.X + cx[3], ry + 6, Fonts.Body, Pal.Muted);
                int cr = Journal2.CrashesLast7(q);
                ui.Text(cr.ToString(), card.X + cx[4], ry + 6, Fonts.Body, cr > 2 ? Pal.Bad : Pal.Muted);
                string todo = q.Status == 2 ? "Browser login" : (q.Status == 3 ? "Unlock on roblox.com" : (cr > 2 ? "Lower graphics" : ""));
                ui.Text(todo, card.X + cx[5], ry + 6, Fonts.Body, Pal.Warn);
                ry += rowH;
            }
            y = card.Bottom + 26;
            ui.Text("Recent events", a.X, y, Fonts.H2, Pal.Text);
            y += 40;
            foreach (JournalEntry e in ev)
            {
                ui.Text(new DateTime(e.Ticks, DateTimeKind.Utc).ToLocalTime().ToString("MM-dd HH:mm"), a.X + 4, y + 2, Fonts.Small, Pal.Faint);
                ui.Text(e.Kind, a.X + 110, y + 2, Fonts.SmallB, e.Kind == "Recovered" || e.Kind == "Relaunch" ? Pal.Good : Pal.Warn);
                ui.Text(Ui.Fit(ui, e.Account + ": " + e.Text, Fonts.Small, a.Width - 260), a.X + 230, y + 2, Fonts.Small, Pal.Muted);
                y += 30;
            }
            ui.EndScroll();
        }

        void ExportEvents()
        {
            using (SaveFileDialog d = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "fleet-events.csv" })
                if (d.ShowDialog(canvas.FindForm()) == DialogResult.OK) { try { Journal2.ExportCsv(d.FileName); Log.Add("Events exported."); } catch (Exception ex) { Log.Add("Could not export: " + ex.Message); } }
        }

        void RemoveBroken()
        {
            List<Account> bad = Store.Accounts.Where(q => q.Status == 2 || q.Status == 3).ToList();
            if (bad.Count == 0) { Log.Add("No broken accounts."); return; }
            canvas.Post(delegate
            {
                if (!Dialogs.Confirm(canvas.FindForm(), "Remove broken accounts", Lang.F("Remove {0} signed-out or locked accounts?", bad.Count), "Remove")) return;
                foreach (Account q in bad) { Store.Accounts.Remove(q); selected.Remove(q.Id); }
                Store.SaveAccounts();
            });
        }
    }
}
