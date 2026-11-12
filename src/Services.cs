using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fleet
{
    public static class Proxies
    {
        public static List<string> Pool()
        {
            return (Store.Cfg.ProxyPool ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList();
        }

        // account i gets pool[(i / perProxy) % pool.Count]
        public static int AssignAll()
        {
            List<string> pool = Pool();
            if (pool.Count == 0) return 0;
            int per = Math.Max(1, Store.Cfg.ProxyPerAccounts), n = 0;
            List<Account> list = Store.Accounts.OrderBy(a => a.Order).ToList();
            for (int i = 0; i < list.Count; i++) { list[i].Proxy = pool[(i / per) % pool.Count]; list[i].ProxyAuto = true; n++; }
            Store.SaveAccounts();
            return n;
        }

        public static void AssignNew(Account a)
        {
            if (!Store.Cfg.ProxyAuto) return;
            List<string> pool = Pool();
            if (pool.Count == 0) return;
            int per = Math.Max(1, Store.Cfg.ProxyPerAccounts);
            int idx = Store.Accounts.Count(x => x != a && x.ProxyAuto);
            a.Proxy = pool[(idx / per) % pool.Count]; a.ProxyAuto = true;
        }

        public static int RemoveAuto()
        {
            int n = 0;
            foreach (Account a in Store.Accounts.Where(x => x.ProxyAuto)) { a.Proxy = ""; a.ProxyAuto = false; n++; }
            Store.SaveAccounts();
            return n;
        }

        // milliseconds, or -1
        public static async Task<int> Test(string proxy)
        {
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                await Roblox.GetBytes("https://www.roblox.com/robots.txt", proxy);
                return (int)sw.ElapsedMilliseconds;
            }
            catch (Exception) { return -1; }
        }
    }

    public static class Hook
    {
        public static async void Send(string title, string text, int color)
        {
            string url = Store.Cfg.Webhook;
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://")) return;
            try
            {
                string body = "{\"embeds\":[{\"title\":" + Q(title) + ",\"description\":" + Q(text) + ",\"color\":" + color + "}]}";
                await Roblox.Post(url, body);
            }
            catch (Exception) { }
        }

        static string Q(string s) { return "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\""; }
        public const int Red = 0xF05A5A, Yellow = 0xF5B43C, Green = 0x4AD282;
    }

    // tray icon, toasts, global hotkeys, start with windows
    public static class Shell
    {
        public static NotifyIcon Tray;

        public static void Toast(string title, string text)
        {
            if (!Store.Cfg.Toasts || Tray == null || !Tray.Visible) return;
            try { Tray.ShowBalloonTip(5000, title, text, ToolTipIcon.Info); } catch (Exception) { }
        }

        public static void SetStartup(bool on)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k == null) return;
                    if (on) k.SetValue("Fleet", "\"" + Application.ExecutablePath + "\" --min"); else k.DeleteValue("Fleet", false);
                }
            }
            catch (Exception ex) { Log.Add("Could not change start with Windows: " + ex.Message); }
        }
    }
}
