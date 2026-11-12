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

        public static string FindRoblox()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions");
            try
            {
                if (Directory.Exists(root))
                    foreach (string d in Directory.GetDirectories(root).OrderByDescending(x => Directory.GetLastWriteTime(x)))
                    {
                        string exe = Path.Combine(d, "RobloxPlayerBeta.exe");
                        if (File.Exists(exe)) return exe;
                    }
            }
            catch (Exception) { }
            return null;
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

        public static async Task LaunchMany(List<Account> accounts, Target shared)
        {
            if (Busy) { Log.Add("A launch is already running."); return; }
            if (accounts.Count == 0) { Log.Add("Pick at least one account."); return; }
            if (Versions.PinnedExe() == null && FindRoblox() == null) { Log.Add("Roblox is not installed for this Windows user. Install it from roblox.com first."); return; }
            if (Store.Cfg.MultiInstance && !MultiActive) SetMultiInstance(true);
            Busy = true;
            try
            {
                Perf.SetGraphics(Store.Cfg.GfxPreset);
                SetFpsCap(Store.Cfg.FpsCap);
                Log.Add("Launching " + accounts.Count + " account(s).");
                bool serial = accounts.Any(x => x.FpsCap > 0);
                SemaphoreSlim gate = new SemaphoreSlim(serial ? 1 : 4);
                List<Task> jobs = new List<Task>();
                for (int i = 0; i < accounts.Count; i++)
                {
                    Account a = accounts[i];
                    Target t = TargetFor(a, shared);
                    if (t.Place <= 0) { Log.Add(a.Label + ": no game chosen. Set a place on the Launch page."); continue; }
                    BusyText = "Starting " + a.Label + " (" + (i + 1) + "/" + accounts.Count + ")";
                    await gate.WaitAsync();
                    jobs.Add(LaunchOne(a, t, gate));
                    if (i < accounts.Count - 1) await Task.Delay(Math.Max(0, Store.Cfg.StaggerMs));
                }
                await Task.WhenAll(jobs.ToArray());
                Log.Add("Launch finished.");
                if (Store.Cfg.HookBatch) Hook.Send("Launch finished", "Started " + accounts.Count + " account(s).", Hook.Green);
            }
            catch (Exception ex) { Log.Add("Launch failed: " + ex.Message); }
            Busy = false;
            BusyText = "";
        }

        static async Task LaunchOne(Account a, Target t, SemaphoreSlim gate)
        {
            try
            {
                foreach (Running old in Live.Where(x => x.Acc == a).ToList()) Kill(old);
                if (Store.Cfg.RefreshCookie)
                {
                    string nc = await Roblox.Rotate(a.Cookie);
                    if (nc != null) { a.Cookie = nc; Store.SaveAccounts(); Log.Add(a.Label + ": cookie refreshed."); }
                }
                string ticket = await Roblox.Ticket(a.Cookie);
                if (ticket == null)
                {
                    a.Status = 2; Store.SaveAccounts();
                    Log.Add(a.Label + ": signed out. Add the account again with a fresh cookie.");
                    Journal2.Add(a, "SignedOut", "Launch failed: the saved login no longer works.");
                    return;
                }
                a.Status = 1;
                long tracker = 100000000000L + (long)(rnd.NextDouble() * 800000000000L);
                long now = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
                string placeUrl = PlaceUrl(t, tracker);
                if (a.FpsCap > 0) SetFpsCap(a.FpsCap);
                string pinned = Versions.PinnedExe();
                if (pinned != null || Store.Cfg.LaunchMethod == "Direct")
                {
                    string exe = pinned ?? FindRoblox();
                    string args = "--app -t " + ticket + " -j \"" + placeUrl + "\" -b " + tracker + " --launchtime=" + now + " --rloc en_us --gloc en_us";
                    ProcessStartInfo psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) };
                    await Task.Run(delegate { using (Process.Start(psi)) { } });
                }
                else
                {
                    string uri = "roblox-player:1+launchmode:play+gameinfo:" + ticket + "+launchtime:" + now
                        + "+placelauncherurl:" + Uri.EscapeDataString(placeUrl) + "+browsertrackerid:" + tracker
                        + "+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
                    await Task.Run(delegate { using (Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true })) { } });
                }
                Log.Add(a.Label + ": starting " + t.Describe() + "...");

                int pid = 0;
                for (int i = 0; i < 300 && pid == 0; i++)
                {
                    await Task.Delay(i < 4 ? 250 : 200);
                    pid = await FindByTracker(tracker);
                }
                a.LastLaunch = DateTime.UtcNow.Ticks;
                Store.SaveAccounts();
                if (pid == 0)
                {
                    Log.Add(a.Label + ": Roblox did not show up within a minute.");
                    Journal2.Add(a, "LaunchFailed", "Roblox started but no window was found in time.");
                    return;
                }
                Running r = new Running { Acc = a, Pid = pid, Spec = t };
                Live.Add(r);
                ApplyPriority(pid);
                Perf.Apply();
                Log.Add(a.Label + ": running (process " + pid + ").");
                if (a.Muted) MuteLater(pid);
                if (a.FpsCap > 0)
                {
                    await Task.Delay(6000);
                    SetFpsCap(Store.Cfg.FpsCap > 0 ? Store.Cfg.FpsCap : -1);
                }
            }
            catch (Exception ex) { Log.Add(a.Label + ": " + ex.Message); }
            finally { gate.Release(); }
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

        static async Task<int> FindByTracker(long tracker)
        {
            string needle = tracker.ToString();
            foreach (KeyValuePair<int, string> kv in await Snapshot())
                if (kv.Value.IndexOf(needle, StringComparison.Ordinal) >= 0) return kv.Key;
            return 0;
        }

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

        public static void Arrange(int monitor, int cols, int gap)
        {
            if (Live.Count == 0) { Log.Add("No Roblox window to arrange."); return; }
            Screen[] screens = Screen.AllScreens;
            Screen sc = screens[Math.Max(0, Math.Min(monitor, screens.Length - 1))];
            Dictionary<uint, IntPtr> wins = Native.WindowsOf(new HashSet<uint>(Live.Select(r => (uint)r.Pid)));
            List<IntPtr> handles = new List<IntPtr>();
            foreach (Running r in Live) { IntPtr h; if (wins.TryGetValue((uint)r.Pid, out h)) handles.Add(h); }
            if (handles.Count == 0) { Log.Add("The Roblox windows are not visible yet."); return; }
            Rectangle area = sc.WorkingArea;
            int c, rows;
            Grid(handles.Count, cols, area.Width / (double)area.Height, out c, out rows);
            int w = (area.Width - gap * (c + 1)) / c, h2 = (area.Height - gap * (rows + 1)) / rows;
            List<Running> ordered = Live.Where(r => wins.ContainsKey((uint)r.Pid)).ToList();
            for (int i = 0; i < handles.Count; i++)
            {
                int cx = i % c, cy = i / c;
                IntPtr h = handles[i];
                Native.ShowWindow(h, 9);
                if (Store.Cfg.LayoutBorderless) Native.SetWindowLong(h, -16, Native.GetWindowLong(h, -16) & ~(0xC00000 | 0x40000));
                if (Store.Cfg.LayoutRename && i < ordered.Count) Native.SetWindowText(h, ordered[i].Acc.Label);
                Native.SetWindowPos(h, Store.Cfg.LayoutTopMost ? (IntPtr)(-1) : IntPtr.Zero, area.X + gap + cx * (w + gap), area.Y + gap + cy * (h2 + gap), w, h2, (Store.Cfg.LayoutTopMost ? 0u : Native.SWP_NOZORDER) | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW | 0x20);
            }
            Log.Add("Arranged " + handles.Count + " window(s).");
        }

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
