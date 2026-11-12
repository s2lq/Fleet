using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Fleet
{
    public static partial class Roblox
    {
        public class Presence2 { public int Type; public long PlaceId; public string GameId = ""; }

        public static async Task<Dictionary<long, Presence2>> PresenceFull(string cookie, List<long> ids)
        {
            Dictionary<long, Presence2> map = new Dictionary<long, Presence2>();
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
                        if (p == null) continue;
                        Presence2 pr = new Presence2 { Type = Convert.ToInt32(p["userPresenceType"]) };
                        if (p.ContainsKey("placeId") && p["placeId"] != null) pr.PlaceId = Convert.ToInt64(p["placeId"]);
                        if (p.ContainsKey("gameId") && p["gameId"] != null) pr.GameId = Convert.ToString(p["gameId"]);
                        map[Convert.ToInt64(p["userId"])] = pr;
                    }
            }
            return map;
        }

        public static async Task<long> Robux(Account a)
        {
            using (HttpResponseMessage resp = await Client(a.Cookie).SendAsync(Req(HttpMethod.Get, "https://economy.roblox.com/v1/users/" + a.UserId + "/currency", a.Cookie)))
            {
                if (!resp.IsSuccessStatusCode) return -1;
                Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(await resp.Content.ReadAsStringAsync());
                return d.ContainsKey("robux") ? Convert.ToInt64(d["robux"]) : -1;
            }
        }

        public static async Task<string> UserIdByName(string name)
        {
            HttpRequestMessage r = new HttpRequestMessage(HttpMethod.Post, "https://users.roblox.com/v1/usernames/users");
            r.Content = new StringContent(js.Serialize(new Dictionary<string, object> { { "usernames", new string[] { name } }, { "excludeBannedUsers", true } }), Encoding.UTF8, "application/json");
            using (HttpResponseMessage resp = await http.SendAsync(r))
            {
                Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(await resp.Content.ReadAsStringAsync());
                System.Collections.ArrayList arr = d["data"] as System.Collections.ArrayList;
                if (arr == null || arr.Count == 0) return null;
                return Convert.ToString(((Dictionary<string, object>)arr[0])["id"]);
            }
        }

        public class PublicServer { public string Id = ""; public int Playing, Max, Ping; public double Fps; }

        public static async Task<List<PublicServer>> Servers(long place)
        {
            List<PublicServer> list = new List<PublicServer>();
            string json = await http.GetStringAsync("https://games.roblox.com/v1/games/" + place + "/servers/Public?sortOrder=Asc&limit=100");
            Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(json);
            System.Collections.ArrayList arr = d["data"] as System.Collections.ArrayList;
            if (arr == null) return list;
            foreach (object o in arr)
            {
                Dictionary<string, object> p = (Dictionary<string, object>)o;
                PublicServer s = new PublicServer { Id = Convert.ToString(p["id"]), Playing = Convert.ToInt32(p["playing"]), Max = Convert.ToInt32(p["maxPlayers"]) };
                if (p.ContainsKey("ping") && p["ping"] != null) s.Ping = Convert.ToInt32(p["ping"]);
                if (p.ContainsKey("fps") && p["fps"] != null) s.Fps = Convert.ToDouble(p["fps"]);
                list.Add(s);
            }
            return list;
        }

        public class GameHit { public string Name = ""; public long Place; public int Players; }

        public static async Task<List<GameHit>> SearchGames(string q)
        {
            List<GameHit> hits = new List<GameHit>();
            string url = "https://apis.roblox.com/search-api/omni-search?searchQuery=" + Uri.EscapeDataString(q) + "&pageType=all";
            Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(await http.GetStringAsync(url));
            System.Collections.ArrayList groups = d.ContainsKey("searchResults") ? d["searchResults"] as System.Collections.ArrayList : null;
            if (groups == null) return hits;
            foreach (object g in groups)
            {
                Dictionary<string, object> gd = (Dictionary<string, object>)g;
                System.Collections.ArrayList contents = gd.ContainsKey("contents") ? gd["contents"] as System.Collections.ArrayList : null;
                if (contents == null) continue;
                foreach (object c in contents)
                {
                    Dictionary<string, object> cd = (Dictionary<string, object>)c;
                    if (!cd.ContainsKey("rootPlaceId") || cd["rootPlaceId"] == null) continue;
                    GameHit h = new GameHit { Name = Convert.ToString(cd["name"]), Place = Convert.ToInt64(cd["rootPlaceId"]) };
                    if (cd.ContainsKey("playerCount") && cd["playerCount"] != null) h.Players = Convert.ToInt32(cd["playerCount"]);
                    hits.Add(h);
                    if (hits.Count >= 8) return hits;
                }
            }
            return hits;
        }

        // asks Roblox if this account may join the private server, without starting anything
        public static async Task<string> CheckPrivate(Account a, long place, string code)
        {
            string url = "https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestPrivateGame&placeId=" + place + "&linkCode=" + code + "&isPlayTogetherGame=false";
            using (HttpResponseMessage resp = await Client(a.Cookie).SendAsync(Req(HttpMethod.Get, url, a.Cookie)))
            {
                string body = await resp.Content.ReadAsStringAsync();
                Match m = Regex.Match(body, "\"status\"\\s*:\\s*(\\d+)");
                int st = m.Success ? int.Parse(m.Groups[1].Value) : -1;
                if (st == 2) return "OK";
                if (st == 6 || st == 12) return "Not allowed";
                return "Link not valid (" + (st < 0 ? ((int)resp.StatusCode).ToString() : st.ToString()) + ")";
            }
        }

        // asks Roblox for a fresh cookie (the old one stops working); null when it refuses
        public static async Task<string> Rotate(string cookie)
        {
            string key = cookie.Length > 40 ? cookie.Substring(cookie.Length - 40) : cookie;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                HttpRequestMessage r = Req(HttpMethod.Post, "https://www.roblox.com/authentication/signoutfromallsessionsandreauthenticate", cookie);
                r.Content = new StringContent("", Encoding.UTF8, "application/x-www-form-urlencoded");
                string token;
                if (csrf.TryGetValue(key, out token)) r.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
                using (HttpResponseMessage resp = await Client(cookie).SendAsync(r))
                {
                    IEnumerable<string> vals;
                    if (resp.Headers.TryGetValues("x-csrf-token", out vals)) foreach (string v in vals) { csrf[key] = v; break; }
                    if (resp.Headers.TryGetValues("Set-Cookie", out vals))
                        foreach (string v in vals)
                        {
                            Match m = Regex.Match(v, @"\.ROBLOSECURITY=([^;]+)");
                            if (m.Success && m.Groups[1].Value.StartsWith("_|WARNING")) return m.Groups[1].Value;
                        }
                    if ((int)resp.StatusCode != 403) return null;
                }
            }
            return null;
        }

        public static async Task<string> Post(string url, string json)
        {
            HttpRequestMessage r = new HttpRequestMessage(HttpMethod.Post, url);
            r.Content = new StringContent(json, Encoding.UTF8, "application/json");
            using (HttpResponseMessage resp = await http.SendAsync(r)) return resp.IsSuccessStatusCode ? null : ((int)resp.StatusCode).ToString();
        }

        public static async Task<bool> Online()
        {
            try { using (HttpResponseMessage r = await http.GetAsync("https://www.roblox.com/robots.txt")) return true; }
            catch (Exception) { return false; }
        }

        public static async Task<string> GetText(string url) { return await http.GetStringAsync(url); }

        public static async Task<byte[]> GetBytes(string url, string proxy) { return await ForProxy(proxy).GetByteArrayAsync(url); }
    }
}
