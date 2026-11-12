using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Fleet
{
    public class Who { public long Id; public string Name = ""; public string Display = ""; }

    public static partial class Roblox
    {
        static readonly HttpClient http = Make();
        static readonly JavaScriptSerializer js = new JavaScriptSerializer();
        static readonly Dictionary<string, string> csrf = new Dictionary<string, string>();
        static readonly Dictionary<long, Bitmap> faces = new Dictionary<long, Bitmap>();
        static readonly HashSet<long> loadingFaces = new HashSet<long>();

        static HttpClient Make()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls;
            HttpClientHandler h = new HttpClientHandler();
            h.UseCookies = false;
            HttpClient c = new HttpClient(h);
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Fleet");
            return c;
        }

        public static string ExtractCookie(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            // people paste all kinds of junk, just grab the cookie part
            Match m = Regex.Match(text, @"_\|WARNING:[^\s;""']+");
            if (m.Success) return m.Value;
            string t = text.Trim();
            return t.Length > 200 && !t.Contains(" ") ? t : null;
        }

        static readonly Dictionary<string, HttpClient> proxied = new Dictionary<string, HttpClient>();

        public static HttpClient ForProxy(string proxy)
        {
            if (string.IsNullOrEmpty(proxy)) return http;
            lock (proxied)
            {
                HttpClient c;
                if (proxied.TryGetValue(proxy, out c)) return c;
                string[] p = proxy.Split(':');
                int port;
                if (p.Length < 2 || !int.TryParse(p[1], out port)) return http;
                HttpClientHandler h = new HttpClientHandler { UseCookies = false };
                WebProxy wp = new WebProxy(p[0], port);
                if (p.Length >= 4) wp.Credentials = new NetworkCredential(p[2], p[3]);
                h.Proxy = wp; h.UseProxy = true;
                c = new HttpClient(h) { Timeout = TimeSpan.FromSeconds(20) };
                c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Fleet");
                proxied[proxy] = c;
                return c;
            }
        }

        static HttpClient Client(string cookie)
        {
            Account a = Store.Accounts.FirstOrDefault(x => x.Cookie == cookie);
            return a != null ? ForProxy(a.Proxy) : http;
        }

        static HttpRequestMessage Req(HttpMethod m, string url, string cookie)
        {
            HttpRequestMessage r = new HttpRequestMessage(m, url);
            if (!string.IsNullOrEmpty(cookie)) r.Headers.TryAddWithoutValidation("Cookie", ".ROBLOSECURITY=" + cookie);
            r.Headers.TryAddWithoutValidation("Referer", "https://www.roblox.com/");
            return r;
        }

        public static async Task<Who> WhoAmI(string cookie)
        {
            // 401 = cookie dead
            using (HttpResponseMessage resp = await Client(cookie).SendAsync(Req(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated", cookie)))
            {
                if ((int)resp.StatusCode == 401) return null;
                if ((int)resp.StatusCode == 403) return new Who { Id = -1 };
                resp.EnsureSuccessStatusCode();
                Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(await resp.Content.ReadAsStringAsync());
                Who w = new Who();
                w.Id = Convert.ToInt64(d["id"]);
                w.Name = Convert.ToString(d["name"]);
                w.Display = d.ContainsKey("displayName") ? Convert.ToString(d["displayName"]) : w.Name;
                return w;
            }
        }

        public static async Task<string> Ticket(string cookie)
        {
            string key = cookie.Length > 40 ? cookie.Substring(cookie.Length - 40) : cookie;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                HttpRequestMessage r = Req(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket", cookie);
                r.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                string token;
                if (csrf.TryGetValue(key, out token)) r.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
                using (HttpResponseMessage resp = await http.SendAsync(r))
                {
                    IEnumerable<string> vals;
                    if (resp.Headers.TryGetValues("x-csrf-token", out vals))
                        foreach (string v in vals) { csrf[key] = v; break; }
                    if (resp.Headers.TryGetValues("rbx-authentication-ticket", out vals))
                        foreach (string v in vals) return v;
                    int code = (int)resp.StatusCode;
                    if (code == 401) return null;
                    // first call always 403s, it only hands out the csrf token
                    if (code == 403 && attempt < 2) continue;
                    if (code == 429) { await Task.Delay(1500); continue; }
                }
            }
            return null;
        }

        public static async Task<Dictionary<long, int>> Presence(string cookie, List<long> ids)
        {
            Dictionary<long, int> map = new Dictionary<long, int>();
            if (ids.Count == 0) return map;
            HttpRequestMessage r = Req(HttpMethod.Post, "https://presence.roblox.com/v1/presence/users", cookie);
            r.Content = new StringContent(js.Serialize(new Dictionary<string, object> { { "userIds", ids } }), Encoding.UTF8, "application/json");
            using (HttpResponseMessage resp = await Client(cookie).SendAsync(r))
            {
                if (!resp.IsSuccessStatusCode) return map;
                Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(await resp.Content.ReadAsStringAsync());
                System.Collections.ArrayList arr = d["userPresences"] as System.Collections.ArrayList;
                if (arr != null)
                    foreach (object o in arr)
                    {
                        Dictionary<string, object> p = o as Dictionary<string, object>;
                        if (p != null) map[Convert.ToInt64(p["userId"])] = Convert.ToInt32(p["userPresenceType"]);
                    }
            }
            return map;
        }

        public static Bitmap Face(long userId)
        {
            if (userId <= 0) return null;
            lock (faces)
            {
                Bitmap b;
                if (faces.TryGetValue(userId, out b)) return b;
                if (loadingFaces.Contains(userId)) return null;
                loadingFaces.Add(userId);
            }
            Task.Run(async delegate
            {
                try
                {
                    string url = "https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds=" + userId + "&size=150x150&format=Png";
                    Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(await http.GetStringAsync(url));
                    System.Collections.ArrayList arr = d["data"] as System.Collections.ArrayList;
                    Dictionary<string, object> first = arr != null && arr.Count > 0 ? arr[0] as Dictionary<string, object> : null;
                    string img = first != null && first.ContainsKey("imageUrl") ? first["imageUrl"] as string : null;
                    if (!string.IsNullOrEmpty(img))
                    {
                        byte[] bytes = await http.GetByteArrayAsync(img);
                        using (MemoryStream ms = new MemoryStream(bytes))
                        using (Bitmap raw = new Bitmap(ms))
                        {
                            Bitmap copy = new Bitmap(raw);
                            // copy so the stream can be disposed
                            lock (faces) faces[userId] = copy;
                        }
                    }
                }
                catch (Exception) { }
                lock (faces) loadingFaces.Remove(userId);
            });
            return null;
        }

        public static long ParsePlace(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            Match m = Regex.Match(text, @"games/(\d+)");
            if (!m.Success) m = Regex.Match(text, @"placeId=(\d+)");
            if (!m.Success) m = Regex.Match(text.Trim(), @"^\d{3,}$");
            long id;
            return m.Success && long.TryParse(m.Groups[m.Groups.Count - 1].Value, out id) ? id : 0;
        }

        public static string ParseLinkCode(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            Match m = Regex.Match(text, @"privateServerLinkCode=([A-Za-z0-9_\-]+)");
            return m.Success ? m.Groups[1].Value : null;
        }
    }
}
