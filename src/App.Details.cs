using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fleet
{
    public partial class App
    {
        Account detail;
        string detailStatus = "";

        public void OpenDetails(Account a)
        {
            detail = a;
            detailStatus = "";
            if (a.DetailsTicks == 0 || (DateTime.UtcNow - new DateTime(a.DetailsTicks, DateTimeKind.Utc)).TotalMinutes > 30) LoadDetails(a);
        }

        async void LoadDetails(Account a)
        {
            detailStatus = "Loading...";
            try { detailStatus = await Roblox.Details(a) ? "" : "Roblox did not answer."; Store.SaveAccounts(); }
            catch (Exception ex) { detailStatus = "Could not load: " + ex.Message; }
        }

        static string Age(string created)
        {
            DateTime d;
            if (!DateTime.TryParse(created, null, System.Globalization.DateTimeStyles.RoundtripKind, out d)) return "unknown";
            int months = (DateTime.UtcNow.Year - d.Year) * 12 + DateTime.UtcNow.Month - d.Month;
            return d.ToString("d MMM yyyy") + "   (" + months / 12 + " y " + months % 12 + " m)";
        }

        void DrawDetails(Ui ui)
        {
            Account a = detail;
            float w = Math.Min(680, ui.W - 40), h = 520;
            float e = ui.Appear("detail", 0);
            RectangleF card = new RectangleF((ui.W - w) / 2, Math.Max(20, (ui.H - h) / 2) + (1f - e) * 24f, w, h);
            ui.Fill(new RectangleF(0, 0, ui.W, ui.H), 0, Color.FromArgb((int)(170 * e), Pal.Bg.R, Pal.Bg.G, Pal.Bg.B));
            if (ui.Pressed && !card.Contains(ui.Mouse)) { detail = null; ui.Forget("detail"); return; }
            ui.Fill(new RectangleF(card.X - 1, card.Y - 1, card.Width + 2, card.Height + 2), 14, Pal.Line);
            ui.Fill(card, 13, Pal.Panel);

            float x = card.X + 32, y = card.Y + 30;
            ui.Avatar(new RectangleF(x, y, 84, 84), a);
            ui.Text(Ui.Fit(ui, a.Label, Fonts.H2, w - 200), x + 104, y + 6, Fonts.H2, Pal.Text);
            ui.Text("@" + a.Name + "   id " + a.UserId, x + 104, y + 36, Fonts.Body, Pal.Muted);
            Running run = Engine.Of(a);
            ui.Chip(StatusText(a, run), x + 104, y + 62, StatusColor(a, run));
            if (a.Premium) ui.Chip("Premium", x + 104 + ui.ChipWidth(StatusText(a, run)) + 6, y + 62, Pal.Warn);
            if (detailStatus.Length > 0)
            {
                if (detailStatus == "Loading...") ui.Spinner(new RectangleF(card.Right - 54, y + 8, 18, 18), Pal.Muted);
                else ui.Text(Ui.Fit(ui, detailStatus, Fonts.Small, 220), card.Right - 260, y + 10, Fonts.Small, Pal.Muted);
            }

            float gy = y + 112;
            float colW = (w - 64) / 2;
            string[,] rows =
            {
                { "Account age", a.Created.Length > 0 ? Age(a.Created) : "..." },
                { "Premium", a.DetailsTicks == 0 ? "..." : (a.Premium ? "Yes" : "No") },
                { "Robux", a.Robux >= 0 ? a.Robux.ToString("N0") : "..." },
                { "Friends", a.Friends >= 0 ? a.Friends.ToString("N0") : "..." },
                { "Followers", a.Followers >= 0 ? a.Followers.ToString("N0") : "..." },
                { "Last launch", Ago(a.LastLaunch) },
                { "Group", string.IsNullOrEmpty(a.Group) ? "none" : a.Group },
                { "Proxy", string.IsNullOrEmpty(a.Proxy) ? "none" : a.Proxy.Split(':')[0] }
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float rx = x + (i % 2) * colW, ry = gy + (i / 2) * 52;
                ui.Text(Lang.T(rows[i, 0]).ToUpper(), rx, ry, Fonts.SmallB, Pal.Muted);
                ui.Text(Ui.Fit(ui, rows[i, 1], Fonts.BodyB, colW - 16), rx, ry + 20, Fonts.BodyB, Pal.Text);
            }
            float fy = gy + 4 * 52 + 6;
            ui.Text("FRIENDS", x, fy, Fonts.SmallB, Pal.Muted);
            float cx = x, cy = fy + 24;
            foreach (string n in a.FriendNames)
            {
                float cw = ui.ChipWidth(n);
                if (cx + cw > card.Right - 32) { cx = x; cy += 28; }
                if (cy > card.Bottom - 90) break;
                ui.Chip(n, cx, cy, Pal.Muted); cx += cw + 6;
            }
            if (a.FriendNames.Count == 0) ui.Text(a.Friends == 0 ? "No friends." : "Not loaded.", x, cy, Fonts.Small, Pal.Faint);

            float by = card.Bottom - 64;
            if (ui.Button("dt.ref", new RectangleF(x, by, 130, 40), "Refresh", Btn.Ghost, "refresh")) LoadDetails(a);
            if (ui.Button("dt.copy", new RectangleF(x + 140, by, 150, 40), "Copy user id", Btn.Ghost, null)) { try { Clipboard.SetText(a.UserId.ToString()); Log.Add("User id copied."); } catch (Exception) { } }
            if (ui.Button("dt.close", new RectangleF(card.Right - 32 - 120, by, 120, 40), "Close", Btn.Primary, null)) { detail = null; ui.Forget("detail"); }
        }
    }
}
