using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Fleet
{
    public static partial class Engine
    {
        class LastServer { public long Place; public string Job = ""; public int Tries; }
        static readonly Dictionary<string, LastServer> lastServers = new Dictionary<string, LastServer>();
        static readonly Dictionary<string, List<DateTime>> relaunches = new Dictionary<string, List<DateTime>>();
        static readonly Dictionary<string, DateTime> blockedUntil = new Dictionary<string, DateTime>();
        static bool watching;

        public static async void WatcherTick()
        {
            if (watching) return;
            watching = true;
            try
            {
                await TrackServers();
                if (Store.Cfg.AutoRelaunch) { await FindKicked(); await RelaunchDead(); }
                else Dead.Clear();
            }
            catch (Exception ex) { Log.Add("Watcher: " + ex.Message); }
            watching = false;
        }

        static async Task TrackServers()
        {
            List<Running> alive = Live.Where(r => r.Acc.UserId > 0 && (r.Spec.Mode == "Game" || r.Spec.Mode == "Server")).ToList();
            if (alive.Count == 0) return;
            Dictionary<long, Roblox.Presence2> pr = await Roblox.PresenceFull(alive[0].Acc.Cookie, alive.Select(r => r.Acc.UserId).Distinct().ToList());
            foreach (Running r in alive)
            {
                Roblox.Presence2 p;
                if (!pr.TryGetValue(r.Acc.UserId, out p) || p.Type != 2 || p.PlaceId <= 0 || p.GameId.Length < 10) continue;
                LastServer ls;
                if (!lastServers.TryGetValue(r.Acc.Id, out ls)) { ls = new LastServer(); lastServers[r.Acc.Id] = ls; }
                if (ls.Job == p.GameId) ls.Tries = 0;
                ls.Place = p.PlaceId; ls.Job = p.GameId;
            }
        }

        // a window that is open but whose account is no longer in a game (kicked, server shut down) for two checks in a row
        static async Task FindKicked()
        {
            List<Running> old = Live.Where(r => (DateTime.Now - r.Started).TotalSeconds > 150 && r.Acc.UserId > 0).ToList();
            if (old.Count == 0) return;
            Dictionary<long, Roblox.Presence2> pr = await Roblox.PresenceFull(old[0].Acc.Cookie, old.Select(r => r.Acc.UserId).Distinct().ToList());
            foreach (Running r in old)
            {
                Roblox.Presence2 p;
                if (!pr.TryGetValue(r.Acc.UserId, out p)) continue;
                if (p.Type == 2) { r.Strikes = 0; continue; }
                if (++r.Strikes < 2) continue;
                Log.Add("Watcher: " + r.Acc.Label + " is not in a game anymore, closing it.");
                Journal2.Add(r.Acc, "Kicked", "Left the game (kick, disconnect or the server shut down).");
                Hook.Send("Kicked or disconnected", r.Acc.Label + " left the game.", Hook.Yellow);
                Shell.Toast("Kicked or disconnected", r.Acc.Label + " left the game, bringing it back.");
                Kill(r);
                r.UserStopped = false;
                Dead.Add(r);
            }
        }

        static bool BreakerAllows(Account a)
        {
            if (!Store.Cfg.Breaker) return true;
            DateTime until;
            if (blockedUntil.TryGetValue(a.Id, out until) && DateTime.Now < until) return false;
            return true;
        }

        static bool BreakerRecord(Account a)
        {
            List<DateTime> l;
            if (!relaunches.TryGetValue(a.Id, out l)) { l = new List<DateTime>(); relaunches[a.Id] = l; }
            l.Add(DateTime.Now);
            l.RemoveAll(t => (DateTime.Now - t).TotalMinutes > 10);
            if (Store.Cfg.Breaker && l.Count >= Math.Max(2, Store.Cfg.BreakerMax))
            {
                blockedUntil[a.Id] = DateTime.Now.AddMinutes(Math.Max(1, Store.Cfg.BreakerCooldownMin));
                l.Clear();
                return true;
            }
            return false;
        }

        public static void BreakerClear(Account a) { blockedUntil.Remove(a.Id); relaunches.Remove(a.Id); }

        static Target Rejoin(Account a, Target spec)
        {
            if (!Store.Cfg.RejoinSame || (spec.Mode != "Game" && spec.Mode != "Server")) return spec;
            LastServer ls;
            if (!lastServers.TryGetValue(a.Id, out ls) || ls.Place <= 0 || ls.Job.Length == 0) return spec;
            if (ls.Tries >= 2) { lastServers.Remove(a.Id); return new Target { Mode = "Game", Place = spec.Place }; }
            ls.Tries++;
            return new Target { Mode = "Server", Place = ls.Place, Job = ls.Job };
        }

        static async Task RelaunchDead()
        {
            List<Running> dead = Dead.ToList();
            Dead.Clear();
            if (dead.Count == 0) return;
            if (!await Roblox.Online()) { Log.Add("Watcher: no internet, will try again."); Dead.AddRange(dead); return; }
            foreach (Running d in dead)
            {
                if (Live.Any(r => r.Acc == d.Acc)) continue;
                if (!BreakerAllows(d.Acc))
                {
                    Log.Add("Watcher: " + d.Acc.Label + " keeps crashing, the crash breaker paused it.");
                    Journal2.Add(d.Acc, "Breaker", "Auto-relaunch paused because it keeps crashing.");
                    continue;
                }
                Log.Add("Watcher: relaunching " + d.Acc.Label + "...");
                Journal2.Add(d.Acc, "Relaunch", "Brought back by the watcher.");
                if (Store.Cfg.HookRelaunch) Hook.Send("Client relaunched", d.Acc.Label + " was brought back.", Hook.Yellow);
                Shell.Toast("Account back", d.Acc.Label + " was relaunched.");
                bool paused = BreakerRecord(d.Acc);
                if (paused && Store.Cfg.LowerGfxOnCrash && Store.Cfg.GfxPreset != "Lowest") { Store.Cfg.GfxPreset = "Lowest"; Store.SaveSettings(); Perf.SetGraphics("Lowest"); Log.Add("Watcher: graphics lowered to Lowest because of the crashes."); }
                if (paused) { Log.Add("Watcher: " + d.Acc.Label + " crashed too often, pausing it for " + Store.Cfg.BreakerCooldownMin + " min."); Hook.Send("Crash breaker", d.Acc.Label + " crashed too often.", Hook.Red); }
                await LaunchMany(new List<Account> { d.Acc }, Rejoin(d.Acc, d.Spec));
                await Task.Delay(Math.Max(500, Store.Cfg.StaggerMs));
            }
        }
    }
}
