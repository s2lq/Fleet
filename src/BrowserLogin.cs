using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Fleet
{
    // opens Edge or Chrome with a throw-away profile, waits for you to log in, reads the cookie and closes it
    public static class BrowserLogin
    {
        static string FindBrowser()
        {
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] paths =
            {
                Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"), Path.Combine(pf, @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"), Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(local, @"Google\Chrome\Application\chrome.exe")
            };
            foreach (string p in paths) if (File.Exists(p)) return p;
            return null;
        }

        public static async Task<string> GetCookie(CancellationToken ct, Action<string> status)
        {
            string exe = FindBrowser();
            if (exe == null) { status("Edge or Chrome was not found."); return null; }
            int port = 9300 + new Random().Next(0, 200);
            string profile = Path.Combine(Path.GetTempPath(), "fleet-login-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Process proc = null;
            try
            {
                proc = Process.Start(new ProcessStartInfo(exe, "--remote-debugging-port=" + port + " --user-data-dir=\"" + profile + "\" --no-first-run --no-default-browser-check --new-window https://www.roblox.com/login") { UseShellExecute = false });
                status("Log in to Roblox in the window that opened. Fleet reads the login by itself.");
                DateTime end = DateTime.Now.AddMinutes(10);
                while (DateTime.Now < end && !ct.IsCancellationRequested)
                {
                    await Task.Delay(1500, ct);
                    string c = await TryRead(port);
                    if (c != null) return c;
                }
                status("Login window timed out.");
                return null;
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception ex) { status("Browser login failed: " + ex.Message); return null; }
            finally
            {
                try { if (proc != null && !proc.HasExited) proc.Kill(); } catch (Exception) { }
                try { Thread.Sleep(400); if (Directory.Exists(profile)) Directory.Delete(profile, true); } catch (Exception) { }
            }
        }

        static async Task<string> TryRead(int port)
        {
            try
            {
                string json = await Roblox.GetText("http://127.0.0.1:" + port + "/json");
                JavaScriptSerializer js = new JavaScriptSerializer();
                foreach (object o in (System.Collections.ArrayList)js.DeserializeObject(json))
                {
                    Dictionary<string, object> tab = (Dictionary<string, object>)o;
                    if (!tab.ContainsKey("webSocketDebuggerUrl")) continue;
                    using (ClientWebSocket ws = new ClientWebSocket())
                    {
                        await ws.ConnectAsync(new Uri(Convert.ToString(tab["webSocketDebuggerUrl"])), CancellationToken.None);
                        byte[] req = Encoding.UTF8.GetBytes("{\"id\":1,\"method\":\"Network.getAllCookies\"}");
                        await ws.SendAsync(new ArraySegment<byte>(req), WebSocketMessageType.Text, true, CancellationToken.None);
                        StringBuilder sb = new StringBuilder();
                        byte[] buf = new byte[65536];
                        WebSocketReceiveResult r;
                        do { r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None); sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count)); } while (!r.EndOfMessage);
                        int i = sb.ToString().IndexOf("\"name\":\".ROBLOSECURITY\"");
                        if (i < 0) continue;
                        int v = sb.ToString().IndexOf("\"value\":\"", i);
                        if (v < 0) continue;
                        v += 9;
                        string val = sb.ToString().Substring(v, sb.ToString().IndexOf('"', v) - v);
                        if (val.StartsWith("_|WARNING")) return val;
                    }
                }
            }
            catch (Exception) { }
            return null;
        }
    }
}
