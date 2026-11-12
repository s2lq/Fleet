using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;

namespace Fleet
{
    public class Target
    {
        public string Mode = "Game";
        public long Place;
        public string Job = "";
        public string LinkCode = "";
        public string Describe()
        {
            if (Mode == "Private") return "private server of place " + Place;
            if (Mode == "Server") return "server " + Job + " of place " + Place;
            return "place " + Place;
        }
    }

    public class Running
    {
        public Account Acc;
        public int Pid;
        public DateTime Started = DateTime.Now;
        public double Cpu;
        public long Ram;
        public TimeSpan lastCpu;
        public DateTime lastAt;
        public Target Spec = new Target();
        public int Strikes;
        public bool UserStopped;
    }

    public static partial class Engine
    {
        public static readonly List<Running> Live = new List<Running>();
        public static bool Busy;
        public static readonly List<Running> Dead = new List<Running>();
        public static string BusyText = "";
        static Mutex singleton;
        static readonly Random rnd = new Random();

        public static bool MultiActive { get { return singleton != null; } }

        public static void SetMultiInstance(bool on)
        {
            if (on && singleton == null)
            {
                try
                {
                    bool created;
                    // roblox refuses a 2nd window if this mutex is taken, so we grab it first
                    singleton = new Mutex(true, "ROBLOX_singletonMutex", out created);
                    if (!created) { singleton.Dispose(); singleton = null; Log.Add("Multi-instance could not start: Roblox is already running. Close all Roblox windows, then turn it on again."); }
                    else Log.Add("Multi-instance is on.");
                }
                catch (Exception ex) { singleton = null; Log.Add("Multi-instance could not start: " + ex.Message); }
            }
            else if (!on && singleton != null)
            {
                try { singleton.ReleaseMutex(); } catch (Exception) { }
                singleton.Dispose();
                singleton = null;
                Log.Add("Multi-instance is off.");
            }
        }

        static string robloxCache; static DateTime robloxCacheAt = DateTime.MinValue;

        public static void ForgetRoblox() { robloxCache = null; }

        public static string FindRoblox()
        {
            if (robloxCache != null && File.Exists(robloxCache) && (DateTime.UtcNow - robloxCacheAt).TotalSeconds < 30) return robloxCache;
            string found = FindRobloxNow();
            robloxCache = found; robloxCacheAt = DateTime.UtcNow;
            return found;
        }

        // the places Roblox keeps its version folders: the user's profile, or Program Files (the all-users install), or a folder picked in the optimizer
        public static List<string> RobloxRoots()
        {
            List<string> roots = new List<string>();
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string[] all = { Store.Cfg.RobloxFolder, Path.Combine(local, "Roblox", "Versions"), Path.Combine(pf, "Roblox", "Versions"), Path.Combine(pf86, "Roblox", "Versions") };
            foreach (string r in all)
            {
                if (string.IsNullOrEmpty(r)) continue;
                string p = r;
                if (Directory.Exists(Path.Combine(p, "Versions"))) p = Path.Combine(p, "Versions");
                if (Directory.Exists(p) && !roots.Contains(p, StringComparer.OrdinalIgnoreCase)) roots.Add(p);
            }
            return roots;
        }

        // every installed player, newest first
        public static List<string> RobloxExes()
        {
            List<string> list = new List<string>();
            try
            {
                foreach (string root in RobloxRoots())
                    foreach (string d in Directory.GetDirectories(root))
                    {
                        string exe = Path.Combine(d, "RobloxPlayerBeta.exe");
                        if (File.Exists(exe)) list.Add(exe);
                    }
            }
            catch (Exception) { }
            return list.OrderByDescending(x => File.GetLastWriteTime(x)).ToList();
        }

        static string FindRobloxNow()
        {
            List<string> all = RobloxExes();
            return all.Count > 0 ? all[0] : null;
        }

        static string PlaceUrl(Target t, long tracker)
        {
            // three flavours of join link: any server, exact job id, private link
            string head = "https://assetgame.roblox.com/game/PlaceLauncher.ashx?";
            if (t.Mode == "Private")
                return head + "request=RequestPrivateGame&browserTrackerId=" + tracker + "&placeId=" + t.Place + "&linkCode=" + t.LinkCode + "&isPlayTogetherGame=false";
            if (t.Mode == "Server")
                return head + "request=RequestGameJob&browserTrackerId=" + tracker + "&placeId=" + t.Place + "&gameId=" + t.Job + "&isPlayTogetherGame=false";
            return head + "request=RequestGame&browserTrackerId=" + tracker + "&placeId=" + t.Place + "&isPlayTogetherGame=false";
        }

        public static Target TargetFor(Account a, Target shared)
        {
            if (!Store.Cfg.UseOwn) return shared;
            SavedServer sv = Store.FindServer(a.ServerId);
            if (sv != null && sv.Place > 0 && sv.Code.Length > 0) return new Target { Mode = "Private", Place = sv.Place, LinkCode = sv.Code };
            long own = Roblox.ParsePlace(a.Place);
            string code = Roblox.ParseLinkCode(a.PrivateLink);
            long linkPlace = Roblox.ParsePlace(a.PrivateLink);
            if (code != null && linkPlace > 0) return new Target { Mode = "Private", Place = linkPlace, LinkCode = code };
            if (own > 0) return new Target { Mode = "Game", Place = own };
            return shared;
        }

        class Prep
        {
            public Account A;
            public Target T;
            public Task<string> Ticket;
            public DateTime Ready = DateTime.MinValue;
            public long Tracker;
        }

        // login ticket for one account, fetched while the others are still starting
        static async Task<string> GetTicket(Prep p, SemaphoreSlim gate)
        {
            await gate.WaitAsync();
            try
            {
                Account a = p.A;
                if (Store.Cfg.RefreshCookie)
                {
                    string nc = await Roblox.Rotate(a.Cookie);
                    if (nc != null) { a.Cookie = nc; Log.Add(a.Label + ": cookie refreshed."); }
                }
                string t = await Roblox.Ticket(a.Cookie);
                p.Ready = DateTime.UtcNow;
                return t;
            }
            catch (Exception) { return null; }
            finally { gate.Release(); }
        }

        public static async Task LaunchMany(List<Account> accounts, Target shared)
        {
            if (Busy) { Log.Add("A launch is already running."); return; }
            if (accounts.Count == 0) { Log.Add("Pick at least one account."); return; }
            if (FindRoblox() == null && !accounts.Any(x => Versions.ExeFor(x) != null)) { Log.Add("Could not find Roblox. Install it from roblox.com, or pick its Versions folder under Tools > Optimizer."); return; }
            if (Store.Cfg.MultiInstance && !MultiActive) SetMultiInstance(true);
            if (Store.Cfg.OptAuto) await Task.Run(() => Optimizer.EnsureFor(FindRoblox()));
            Busy = true;
            try
            {
                Roblox.Warm();
                Detector.Reset();
                Perf.SetGraphics(Store.Cfg.GfxPreset);
                SetFpsCap(Store.Cfg.FpsCap);
                Log.Add("Launching " + accounts.Count + " account(s).");
                bool serial = accounts.Any(x => x.FpsCap > 0);

                // 1. every login ticket is requested at once (a few at a time), so the network waits overlap
                bool once = Store.Cfg.LaunchAtOnce && !serial;
                SemaphoreSlim ticketGate = new SemaphoreSlim(once ? Math.Min(16, Math.Max(6, accounts.Count)) : 6);
                List<Prep> preps = new List<Prep>();
                foreach (Account a in accounts)
                {
                    Target t = TargetFor(a, shared);
                    if (t.Place <= 0) { Log.Add(a.Label + ": no game chosen. Set a place on the Launch page."); continue; }
                    Prep p = new Prep { A = a, T = t };
                    p.Ticket = GetTicket(p, ticketGate);
                    preps.Add(p);
                }

                // 2a. all at once: every client starts the same moment, as soon as its ticket is ready
                List<Task> finishing = new List<Task>();
                if (once)
                {
                    BusyText = "Starting " + preps.Count + " accounts at once";
                    Task<int>[] founds = await Task.WhenAll(preps.Select(p => StartOne(p)).ToArray());
                    for (int i = 0; i < preps.Count; i++) if (founds[i] != null) finishing.Add(FinishOne(preps[i], founds[i]));
                }

                // 2b. or a short time apart; finding each window happens in the background
                for (int i = 0; !once && i < preps.Count; i++)
                {
                    BusyText = "Starting " + preps[i].A.Label + " (" + (i + 1) + "/" + preps.Count + ")";
                    Task<int> found = await StartOne(preps[i]);
                    if (found != null)
                    {
                        Task fin = FinishOne(preps[i], found);
                        finishing.Add(fin);
                        if (serial) await fin;
                    }
                    if (!serial && i < preps.Count - 1) await Task.Delay(Math.Max(0, Store.Cfg.StaggerMs));
                }
                await Task.WhenAll(finishing.ToArray());
                Store.SaveAccounts();
                Perf.Apply();
                Log.Add("Launch finished.");
                if (Store.Cfg.HookBatch) Hook.Send("Launch finished", "Started " + accounts.Count + " account(s).", Hook.Green);
            }
            catch (Exception ex) { Log.Add("Launch failed: " + ex.Message); }
            Busy = false;
            BusyText = "";
        }

        // starts the Roblox process; returns what will become its process id, or null when it could not start
        static async Task<Task<int>> StartOne(Prep p)
        {
            Account a = p.A; Target t = p.T;
            try
            {
                foreach (Running old in Live.Where(x => x.Acc == a).ToList()) Kill(old);
                string ticket = await p.Ticket;
                if (ticket != null && (DateTime.UtcNow - p.Ready).TotalSeconds > 40) ticket = await Roblox.Ticket(a.Cookie);   // tickets go stale
                if (ticket == null)
                {
                    a.Status = 2;
                    Log.Add(a.Label + ": signed out. Add the account again with a fresh cookie.");
                    Journal2.Add(a, "SignedOut", "Launch failed: the saved login no longer works.");
                    return null;
                }
                a.Status = 1;
                long tracker = 100000000000L + (long)(rnd.NextDouble() * 800000000000L);
                p.Tracker = tracker;
                long now = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
                string placeUrl = PlaceUrl(t, tracker);
                if (a.FpsCap > 0) SetFpsCap(a.FpsCap);
                string pinned = Versions.ExeFor(a);
                bool proxyEnv = Store.Cfg.GameProxyEnv && !string.IsNullOrEmpty(a.Proxy);
                ProcessStartInfo psi;
                if (pinned != null || Store.Cfg.LaunchMethod == "Direct" || proxyEnv)
                {
                    string exe = pinned ?? FindRoblox();
                    string args = "--app -t " + ticket + " -j \"" + placeUrl + "\" -b " + tracker + " --launchtime=" + now + " --rloc en_us --gloc en_us";
                    psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) };
                    if (proxyEnv)
                    {
                        string[] px = a.Proxy.Split(':');
                        string url = "http://" + (px.Length >= 4 ? px[2] + ":" + px[3] + "@" : "") + px[0] + ":" + (px.Length > 1 ? px[1] : "80");
                        psi.EnvironmentVariables["HTTP_PROXY"] = url; psi.EnvironmentVariables["HTTPS_PROXY"] = url; psi.EnvironmentVariables["ALL_PROXY"] = url;
                    }
                }
                else
                {
                    string uri = "roblox-player:1+launchmode:play+gameinfo:" + ticket + "+launchtime:" + now
                        + "+placelauncherurl:" + Uri.EscapeDataString(placeUrl) + "+browsertrackerid:" + tracker
                        + "+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
                    psi = new ProcessStartInfo(uri) { UseShellExecute = true };
                }
                Task<int> found = Detector.Expect(tracker);      // listen before starting, so nothing is missed
                try { await Task.Run(delegate { using (Process.Start(psi)) { } }); }
                catch (Exception ex) { Detector.Forget(tracker); Log.Add(a.Label + ": could not start Roblox (" + ex.Message + ")."); return null; }
                Log.Add(a.Label + ": starting " + t.Describe() + "...");
                return found;
            }
            catch (Exception ex) { Log.Add(a.Label + ": " + ex.Message); return null; }
        }

        // waits for the window of a started client and registers it
        static async Task FinishOne(Prep p, Task<int> found)
        {
            Account a = p.A;
            try
            {
                int pid = 0;
                Task first = await Task.WhenAny(found, Task.Delay(60000));
                if (first == found) pid = found.Result; else Detector.Forget(p.Tracker);
                a.LastLaunch = DateTime.UtcNow.Ticks;
                if (pid == 0)
                {
                    Log.Add(a.Label + ": Roblox did not show up within a minute.");
                    Journal2.Add(a, "LaunchFailed", "Roblox started but no window was found in time.");
                    return;
                }
                Running r = new Running { Acc = a, Pid = pid, Spec = p.T };
                Live.Add(r);
                ApplyPriority(pid);
                Log.Add(a.Label + ": running (process " + pid + ").");
                if (a.Muted) MuteLater(pid);
                if (a.FpsCap > 0)
                {
                    await Task.Delay(6000);
                    SetFpsCap(Store.Cfg.FpsCap > 0 ? Store.Cfg.FpsCap : -1);
                }
            }
            catch (Exception ex) { Log.Add(a.Label + ": " + ex.Message); }
        }

        static async void MuteLater(int pid)
        {
            for (int i = 0; i < 4; i++) { await Task.Delay(i == 0 ? 3000 : 6000); Audio.SetMute(pid, true); }
        }

        static Task<List<KeyValuePair<int, string>>> snap;
        static DateTime snapAt = DateTime.MinValue;

        static Task<List<KeyValuePair<int, string>>> Snapshot()
        {
            // one wmi query shared by everyone who is waiting, they are slow
            if (snap != null && (DateTime.UtcNow - snapAt).TotalMilliseconds < 180) return snap;
            snapAt = DateTime.UtcNow;
            snap = Task.Run(delegate
            {
                List<KeyValuePair<int, string>> list = new List<KeyValuePair<int, string>>();
                try
                {
                    using (System.Management.ManagementObjectSearcher s = new System.Management.ManagementObjectSearcher(
                        "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'RobloxPlayerBeta.exe'"))
                    using (System.Management.ManagementObjectCollection res = s.Get())
                        foreach (System.Management.ManagementObject mo in res)
                        {
                            try
                            {
                                object cl = mo["CommandLine"], id = mo["ProcessId"];
                                if (cl != null && id != null) list.Add(new KeyValuePair<int, string>(Convert.ToInt32(id), cl.ToString()));
                            }
                            finally { mo.Dispose(); }
                        }
                }
                catch (Exception) { }
                return list;
            });
            return snap;
        }

        public static Task<List<KeyValuePair<int, string>>> FreshSnapshot() { snapAt = DateTime.MinValue; return Snapshot(); }

        public static void Kill(Running r)
        {
            r.UserStopped = true;
            try { using (Process p = Process.GetProcessById(r.Pid)) p.Kill(); } catch (Exception) { }
            Live.Remove(r);
            Log.Add(r.Acc.Label + ": stopped.");
        }

        public static void StopAll()
        {
            foreach (Running r in Live.ToList()) Kill(r);
        }

        public static Running Of(Account a) { return Live.FirstOrDefault(r => r.Acc == a); }

        public static void Tick()
        {
            // cpu as % of the whole pc, not of one core
            int cores = Math.Max(1, Environment.ProcessorCount);
            foreach (Running r in Live.ToList())
            {
                try
                {
                    using (Process p = Process.GetProcessById(r.Pid))
                    {
                        if (p.HasExited) throw new InvalidOperationException();
                        TimeSpan cpu = p.TotalProcessorTime;
                        DateTime now = DateTime.UtcNow;
                        if (r.lastAt != default(DateTime))
                        {
                            double secs = (now - r.lastAt).TotalSeconds;
                            if (secs > 0.2) r.Cpu = Math.Min(100, (cpu - r.lastCpu).TotalSeconds / secs / cores * 100.0);
                        }
                        r.lastCpu = cpu;
                        r.lastAt = now;
                        p.Refresh();
                        r.Ram = p.WorkingSet64;
                    }
                }
                catch (Exception)
                {
                    Live.Remove(r);
                    Log.Add(r.Acc.Label + ": the window was closed.");
                    if (!r.UserStopped) { Dead.Add(r); Journal2.Add(r.Acc, "Crash", "Its Roblox window disappeared (ran " + (int)(DateTime.Now - r.Started).TotalMinutes + " min)."); Hook.Send("Client closed", r.Acc.Label + " closed unexpectedly.", Hook.Red); }
                }
            }
        }

        static string SettingsFile
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "GlobalBasicSettings_13.xml"); }
        }

        public static void SetFpsCap(int cap)
        {
            if (cap == 0) return;
            try
            {
                string f = SettingsFile;
                // file only exists after roblox ran once
                if (!File.Exists(f)) { Log.Add("FPS cap: start Roblox once and close it so it creates its settings file."); return; }
                string xml = File.ReadAllText(f);
                Regex has = new Regex(@"(<int name=""FramerateCap"">)-?\d+(</int>)");
                if (has.IsMatch(xml)) xml = has.Replace(xml, "${1}" + cap + "${2}");
                else xml = xml.Replace("</Properties>", "<int name=\"FramerateCap\">" + cap + "</int></Properties>");
                File.WriteAllText(f, xml);
            }
            catch (Exception ex) { Log.Add("FPS cap could not be written: " + ex.Message); }
        }

        public static void ApplyPriority(int pid)
        {
            ProcessPriorityClass c;
            switch (Store.Cfg.Priority)
            {
                case "BelowNormal": c = ProcessPriorityClass.BelowNormal; break;
                case "Idle": c = ProcessPriorityClass.Idle; break;
                default: return;
            }
            try { using (Process p = Process.GetProcessById(pid)) p.PriorityClass = c; } catch (Exception) { }
        }

        public static void ApplyPriorityAll()
        {
            foreach (Running r in Live)
            {
                try
                {
                    using (Process p = Process.GetProcessById(r.Pid))
                    {
                        string s = Store.Cfg.Priority;
                        p.PriorityClass = s == "Idle" ? ProcessPriorityClass.Idle : (s == "BelowNormal" ? ProcessPriorityClass.BelowNormal : ProcessPriorityClass.Normal);
                    }
                }
                catch (Exception) { }
            }
        }

        public static long TrimMemory()
        {
            long before = Live.Sum(r => r.Ram);
            foreach (Running r in Live)
            {
                try { using (Process p = Process.GetProcessById(r.Pid)) Native.EmptyWorkingSet(p.Handle); } catch (Exception) { }
            }
            Tick();
            long freed = Math.Max(0, before - Live.Sum(r => r.Ram));
            Log.Add("Memory trimmed: about " + (freed >> 20) + " MB handed back.");
            return freed;
        }

        public static void Arrange(int monitor, int cols, int gap) { ArrangeNow(true); }

        // not pretty maths but it fits ok
        public static void Grid(int n, int cols, double aspect, out int c, out int rows)
        {
            n = Math.Max(1, n);
            c = cols > 0 ? Math.Min(cols, n) : (int)Math.Ceiling(Math.Sqrt(n * aspect / 1.0));
            c = Math.Max(1, Math.Min(c, n));
            rows = (int)Math.Ceiling(n / (double)c);
        }
    }
}
