using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Fleet
{
    public static partial class Roblox
    {
        static async Task<Dictionary<string, object>> Json(Account a, string url)
        {
            using (HttpResponseMessage resp = await Client(a.Cookie).SendAsync(Req(HttpMethod.Get, url, a.Cookie)))
            {
                if (!resp.IsSuccessStatusCode) return null;
                return js.Deserialize<Dictionary<string, object>>(await resp.Content.ReadAsStringAsync());
            }
        }

        // age, Premium, friends and followers of one account
        public static async Task<bool> Details(Account a)
        {
            if (a.UserId <= 0) return false;
            bool any = false;
            Dictionary<string, object> u = await Json(a, "https://users.roblox.com/v1/users/" + a.UserId);
            if (u != null && u.ContainsKey("created")) { a.Created = Convert.ToString(u["created"]); any = true; }
            Dictionary<string, object> f = await Json(a, "https://friends.roblox.com/v1/users/" + a.UserId + "/friends/count");
            if (f != null && f.ContainsKey("count")) { a.Friends = Convert.ToInt32(f["count"]); any = true; }
            Dictionary<string, object> fo = await Json(a, "https://friends.roblox.com/v1/users/" + a.UserId + "/followers/count");
            if (fo != null && fo.ContainsKey("count")) { a.Followers = Convert.ToInt32(fo["count"]); any = true; }
            using (HttpResponseMessage resp = await Client(a.Cookie).SendAsync(Req(HttpMethod.Get, "https://premiumfeatures.roblox.com/v1/users/" + a.UserId + "/validate-membership", a.Cookie)))
                if (resp.IsSuccessStatusCode) { a.Premium = (await resp.Content.ReadAsStringAsync()).Trim().ToLower() == "true"; any = true; }
            Dictionary<string, object> list = await Json(a, "https://friends.roblox.com/v1/users/" + a.UserId + "/friends");
            if (list != null && list.ContainsKey("data"))
            {
                a.FriendNames.Clear();
                foreach (object o in (System.Collections.ArrayList)list["data"])
                {
                    Dictionary<string, object> d = (Dictionary<string, object>)o;
                    if (d.ContainsKey("name")) a.FriendNames.Add(Convert.ToString(d["name"]));
                    if (a.FriendNames.Count >= 14) break;
                }
            }
            if (any) a.DetailsTicks = DateTime.UtcNow.Ticks;
            return any;
        }
    }
}
