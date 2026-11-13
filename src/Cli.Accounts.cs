using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Fleet
{
    public static partial class Cli
    {
        // ---------------------------------------------------------------- picking accounts
        static string StatusText(Account a) { return a.Status == 1 ? "working" : (a.Status == 2 ? "signed out" : (a.Status == 3 ? "locked" : "unchecked")); }

        // the accounts a command is about: names / ids on the line, or --all, --group, --tag, --running, --signed-out
        static List<Account> Pick(Opts o, out string error)
        {
            error = null;
            List<string> names = o.Args.ToList();
            if (o.Has("accounts")) names.AddRange(o.Get("accounts", "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            IEnumerable<Account> all = Store.Accounts.OrderBy(a => a.Order);
            if (names.Count > 0)
            {
                List<Account> list = new List<Account>();
                foreach (string raw in names)
                {
                    string n = raw.Trim();
                    List<Account> hit = Match(n, all);
                    if (hit.Count == 0) { error = "No account matches " + n + "."; return list; }
                    if (hit.Count > 1 && !n.Contains("*")) { error = n + " matches more than one account: " + string.Join(", ", hit.Select(h => h.Label).ToArray()); return list; }
                    foreach (Account h in hit) if (!list.Contains(h)) list.Add(h);
                }
                return list;
            }
            if (o.Has("all")) return all.ToList();
            bool filtered = false;
            if (o.Has("group")) { string g = o.Get("group", ""); all = all.Where(a => string.Equals(a.Group, g, StringComparison.OrdinalIgnoreCase)); filtered = true; }
            if (o.Has("tag")) { string t = o.Get("tag", ""); all = all.Where(a => a.TagList().Contains(t, StringComparer.OrdinalIgnoreCase)); filtered = true; }
            if (o.Has("running")) { all = all.Where(a => Engine.Of(a) != null); filtered = true; }
            if (o.Has("signed-out")) { all = all.Where(a => a.Status == 2 || a.Status == 3); filtered = true; }
            if (!filtered) { error = "Say which accounts: a name, --all, --group G, --tag T or --running."; return new List<Account>(); }
            return all.ToList();
        }

        static List<Account> Match(string n, IEnumerable<Account> all)
        {
            if (n.Contains("*"))
            {
                Regex rx = new Regex("^" + Regex.Escape(n).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase);
                return all.Where(a => rx.IsMatch(a.Name ?? "") || rx.IsMatch(a.Alias ?? "") || rx.IsMatch(a.Display ?? "")).ToList();
            }
            List<Account> exact = all.Where(a => n.Equals(a.Name, StringComparison.OrdinalIgnoreCase) || n.Equals(a.Alias, StringComparison.OrdinalIgnoreCase) || n.Equals(a.Display, StringComparison.OrdinalIgnoreCase) || n == a.Id || n == a.UserId.ToString()).ToList();
            if (exact.Count > 0) return exact;
            List<Account> pre = all.Where(a => (a.Name ?? "").StartsWith(n, StringComparison.OrdinalIgnoreCase) || (a.Alias ?? "").StartsWith(n, StringComparison.OrdinalIgnoreCase)).ToList();
            if (pre.Count > 0) return pre;
            return all.Where(a => (a.Name ?? "").IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0 || (a.Alias ?? "").IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        static Dictionary<string, object> AccountRow(Account a, bool cookies)
        {
            Running r = Engine.Of(a);
            string ck = a.Cookie ?? "";
            return Row("id", a.Id, "name", a.Name, "display", a.Display, "alias", a.Alias, "userId", a.UserId, "group", a.Group, "tags", a.Tags, "color", a.Color, "place", a.Place,
                "privateLink", a.PrivateLink, "server", ServerName(a.ServerId), "status", StatusText(a), "running", r != null, "pid", r != null ? r.Pid : 0, "robux", a.Robux, "premium", a.Premium,
                "muted", a.Muted, "fpsCap", a.FpsCap, "proxy", a.Proxy, "version", a.RobloxVersion, "note", a.Note, "created", a.Created, "friends", a.Friends, "followers", a.Followers,
                "cookie", cookies ? ck : (ck.Length > 10 ? "..." + ck.Substring(ck.Length - 6) : ""));
        }

        static string ServerName(string id) { SavedServer s = Store.FindServer(id); return s == null ? "" : s.Name; }

        static Res NeedAccounts(Opts o, out List<Account> list)
        {
            string err;
            list = Pick(o, out err);
            if (err != null) return Err(err);
            if (list.Count == 0) return Err("No accounts matched.");
            return null;
        }

        // ---------------------------------------------------------------- account
        static void RegisterAccounts()
        {
            Add("account list", "account list [--group G] [--tag T] [--running] [--json]", "All accounts, or the ones that match.", delegate(Opts o)
            {
                List<Account> list;
                string err = null;
                if (o.Args.Count == 0 && !o.Has("all") && !o.Has("group") && !o.Has("tag") && !o.Has("running") && !o.Has("signed-out")) list = Store.Accounts.OrderBy(a => a.Order).ToList();
                else { list = Pick(o, out err); if (err != null) return Err(err); }
                return Table(o, new string[] { "name", "alias", "group", "status", "running", "robux", "server" }, list.Select(a => AccountRow(a, o.Has("cookies"))).ToList());
            });
            Add("account info", "account info NAME [--cookies]", "Everything Fleet knows about one account.", delegate(Opts o)
            {
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                if (o.Has("json")) return Json(l.Select(a => AccountRow(a, o.Has("cookies"))).ToList());
                StringBuilder sb = new StringBuilder();
                foreach (Account a in l)
                {
                    Dictionary<string, object> d = AccountRow(a, o.Has("cookies"));
                    sb.AppendLine(a.Label);
                    foreach (KeyValuePair<string, object> kv in d) sb.AppendLine("  " + kv.Key.PadRight(12) + Clean(Convert.ToString(kv.Value)));
                }
                return Ok(sb.ToString().TrimEnd());
            });
            Add("account add", "account add --cookie TEXT | --file PATH | --stdin [--group G] [--alias A] [--note N]", "Add accounts from a cookie, a list file (also other managers' exports) or standard input.", delegate(Opts o)
            {
                App app = TheApp();
                List<Imp> items = new List<Imp>();
                if (o.Has("cookie")) { string c = Roblox.ExtractCookie(o.Get("cookie", "")); if (c != null) items.Add(new Imp { Cookie = c }); }
                if (o.Has("file")) { string f = o.Get("file", ""); if (!File.Exists(f)) return Err("No such file: " + f); items.AddRange(Importer.FromFile(f)); }
                if (o.Has("stdin"))
                    foreach (string line in o.Stdin.Replace("\r", "").Split('\n')) { string c = Roblox.ExtractCookie(line); if (c != null && !items.Any(x => x.Cookie == c)) items.Add(new Imp { Cookie = c }); }
                if (items.Count == 0) return Err("No cookie found. Give --cookie, --file or --stdin.");
                foreach (Imp i in items) { if (o.Has("group")) i.Group = o.Get("group", ""); if (o.Has("alias") && items.Count == 1) i.Alias = o.Get("alias", ""); if (o.Has("note")) i.Note = o.Get("note", ""); }
                int before = Store.Accounts.Count;
                Task t = app.AddImported(items);
                return new Res { Out = "Checking " + items.Count + " login(s)...", Done = delegate { return t.IsCompleted; }, After = delegate { return "Added " + (Store.Accounts.Count - before) + " new account(s); " + Store.Accounts.Count + " in total."; } };
            });
            Add("account remove", "account remove NAME... --yes", "Remove accounts from Fleet (stops them first). Needs --yes.", delegate(Opts o)
            {
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                if (!o.Has("yes")) return Err("This would remove " + l.Count + " account(s): " + string.Join(", ", l.Select(a => a.Label).ToArray()) + ". Add --yes to do it.");
                foreach (Account a in l) { foreach (Running r in Engine.Live.Where(x => x.Acc == a).ToList()) Engine.Kill(r); Store.Accounts.Remove(a); }
                Store.SaveAccounts();
                return Ok("Removed " + l.Count + ".");
            });
            Add("account set", "account set NAME... key=value ...", "Change fields: alias note group tags color place link server proxy fps version password.", delegate(Opts o)
            {
                List<string> sets = o.Args.Where(a => a.Contains("=")).ToList();
                o.Args.RemoveAll(a => a.Contains("="));
                if (sets.Count == 0) return Err("Give at least one key=value, for example group=Farm.");
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                foreach (string kv in sets)
                {
                    int eq = kv.IndexOf('='); string k = kv.Substring(0, eq).ToLowerInvariant(), v = kv.Substring(eq + 1);
                    foreach (Account a in l)
                    {
                        switch (k)
                        {
                            case "alias": a.Alias = v; break;
                            case "note": a.Note = v; break;
                            case "group": a.Group = v; break;
                            case "tags": a.Tags = v; break;
                            case "color": if (v.Length > 0 && !ColorNamesCli.Contains(v, StringComparer.OrdinalIgnoreCase)) return Err("Colours: " + string.Join(", ", ColorNamesCli)); a.Color = v.Length == 0 ? "" : ColorNamesCli.First(c => c.Equals(v, StringComparison.OrdinalIgnoreCase)); break;
                            case "place": a.Place = v; break;
                            case "link": a.PrivateLink = v; break;
                            case "server": { SavedServer s = v.Length == 0 ? null : Store.Servers.FirstOrDefault(x => x.Name.Equals(v, StringComparison.OrdinalIgnoreCase)); if (v.Length > 0 && s == null) return Err("No saved server called " + v + "."); a.ServerId = s == null ? "" : s.Id; break; }
                            case "proxy": a.Proxy = v; a.ProxyAuto = false; break;
                            case "fps": { int n; int.TryParse(v, out n); a.FpsCap = Math.Max(0, n); break; }
                            case "version": a.RobloxVersion = v; break;
                            case "password": a.Password = v; break;
                            case "muted": a.Muted = v == "true" || v == "1" || v == "on"; break;
                            default: return Err("Unknown field " + k + ".");
                        }
                    }
                }
                Store.SaveAccounts();
                return Ok("Updated " + l.Count + " account(s).");
            });
            Add("account tag", "account tag NAME... --add T [--remove T]", "Add or remove tags.", delegate(Opts o)
            {
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                foreach (Account a in l)
                {
                    List<string> t = a.TagList();
                    if (o.Has("add")) foreach (string x in o.Get("add", "").Split(',')) if (x.Trim().Length > 0 && !t.Contains(x.Trim(), StringComparer.OrdinalIgnoreCase)) t.Add(x.Trim());
                    if (o.Has("remove")) foreach (string x in o.Get("remove", "").Split(',')) t.RemoveAll(y => y.Equals(x.Trim(), StringComparison.OrdinalIgnoreCase));
                    a.Tags = string.Join(",", t.ToArray());
                }
                Store.SaveAccounts();
                return Ok("ok");
            });
            Add("account mute", "account mute NAME...", "Mute the Roblox sound of these accounts.", delegate(Opts o) { return Mute(o, true); });
            Add("account unmute", "account unmute NAME...", "Unmute them.", delegate(Opts o) { return Mute(o, false); });
            Add("account check", "account check [NAME...|--all] [--wait]", "Check that the saved logins still work.", delegate(Opts o)
            {
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                App app = TheApp();
                Task t = CheckAll(app, l);
                if (!o.Has("wait")) return Ok("Checking " + l.Count + " login(s)...");
                return new Res { Done = delegate { return t.IsCompleted; }, After = delegate { return Summary(l); } };
            });
            Add("account robux", "account robux [NAME...|--all]", "Refresh the Robux balance.", delegate(Opts o)
            {
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                App app = TheApp();
                Task t = Each(l, delegate(Account a) { return app.RefreshRobux(a); });
                return new Res { Done = delegate { return t.IsCompleted; }, After = delegate { return string.Join("\n", l.Select(a => a.Label + ": " + a.Robux).ToArray()); } };
            });
            Add("account details", "account details NAME...", "Load age, Premium, friends and followers.", delegate(Opts o)
            {
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                Task t = Each(l, delegate(Account a) { return Roblox.Details(a); });
                return new Res { Done = delegate { return t.IsCompleted; }, After = delegate { Store.SaveAccounts(); return string.Join("\n", l.Select(a => a.Label + ": created " + a.Created + ", premium " + a.Premium + ", friends " + a.Friends + ", followers " + a.Followers).ToArray()); } };
            });
            Add("account refresh-cookie", "account refresh-cookie NAME...", "Ask Roblox for a fresh cookie (the old one stops working).", delegate(Opts o)
            {
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                StringBuilder sb = new StringBuilder();
                Task t = Each(l, async delegate(Account a)
                {
                    string c = await Roblox.Rotate(a.Cookie);
                    if (c != null) { a.Cookie = c; a.Status = 1; Journal2.Add(a, "Recovered", "Cookie refreshed."); sb.AppendLine(a.Label + ": refreshed."); }
                    else sb.AppendLine(a.Label + ": Roblox would not refresh it. Use a browser login.");
                });
                return new Res { Done = delegate { return t.IsCompleted; }, After = delegate { Store.SaveAccounts(); return sb.ToString().TrimEnd(); } };
            });
            Add("account sort", "account sort [--by name|robux|group|status] [--desc]", "Reorder the account list.", delegate(Opts o)
            {
                string by = o.Get("by", "name").ToLowerInvariant();
                IEnumerable<Account> q = Store.Accounts;
                switch (by)
                {
                    case "robux": q = q.OrderBy(a => a.Robux); break;
                    case "group": q = q.OrderBy(a => a.Group ?? "").ThenBy(a => a.Label); break;
                    case "status": q = q.OrderBy(a => a.Status).ThenBy(a => a.Label); break;
                    default: q = q.OrderBy(a => a.Label, StringComparer.OrdinalIgnoreCase); break;
                }
                List<Account> sorted = q.ToList();
                if (o.Has("desc")) sorted.Reverse();
                for (int i = 0; i < sorted.Count; i++) sorted[i].Order = i;
                Store.SaveAccounts();
                return Ok("Sorted by " + by + ".");
            });
            Add("account rename", "account rename NAME... --pattern \"Alt {n}\" [--start 1]", "Give accounts an alias from a pattern. {n} is a number.", delegate(Opts o)
            {
                string pat = o.Get("pattern", "");
                if (pat.Length == 0) return Err("Give --pattern.");
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                int n = o.Int("start", 1);
                foreach (Account a in l) a.Alias = pat.Replace("{n}", (n++).ToString());
                Store.SaveAccounts();
                return Ok("Renamed " + l.Count + ".");
            });
            Add("account import", "account import FILE [--password P]", "Import a list from another manager, or a Fleet / AltHub transfer file.", delegate(Opts o)
            {
                if (o.Args.Count == 0) return Err("Give the file.");
                string f = o.Args[0];
                if (!File.Exists(f)) return Err("No such file: " + f);
                byte[] head = new byte[4];
                using (FileStream fs = File.OpenRead(f)) fs.Read(head, 0, 4);
                if (Encoding.ASCII.GetString(head) == "FLT1")
                {
                    if (!o.Has("password")) return Err("That is an encrypted Fleet file. Give --password.");
                    int added = Store.ImportTransfer(f, o.Get("password", ""));
                    return Ok("Imported " + added + " new account(s).");
                }
                List<Imp> items = Importer.FromFile(f);
                if (items.Count == 0) return Err("No Roblox cookies were found in that file.");
                int before = Store.Accounts.Count;
                Task t = TheApp().AddImported(items);
                return new Res { Out = "Checking " + items.Count + " login(s)...", Done = delegate { return t.IsCompleted; }, After = delegate { return "Added " + (Store.Accounts.Count - before) + " new account(s)."; } };
            });
            Add("account export", "account export FILE --password P", "Export every account (with cookies) to an encrypted .fleet file.", delegate(Opts o)
            {
                if (o.Args.Count == 0 || !o.Has("password")) return Err("Give the file and --password.");
                Store.ExportTransfer(o.Args[0], o.Get("password", ""));
                return Ok("Exported " + Store.Accounts.Count + " account(s) to " + o.Args[0] + ".");
            });
        }

        static readonly string[] ColorNamesCli = { "Red", "Orange", "Yellow", "Green", "Teal", "Blue", "Purple", "Pink" };

        static Res Mute(Opts o, bool mute)
        {
            List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
            foreach (Account a in l) { a.Muted = mute; Running r = Engine.Of(a); if (r != null) Audio.SetMute(r.Pid, mute); }
            Store.SaveAccounts();
            return Ok((mute ? "Muted " : "Unmuted ") + l.Count + ".");
        }

        static async Task Each(List<Account> list, Func<Account, Task> work)
        {
            foreach (Account a in list) { try { await work(a); } catch (Exception ex) { Log.Add(a.Label + ": " + ex.Message); } await Task.Delay(250); }
        }

        static async Task Each(List<Account> list, Func<Account, Task<bool>> work)
        {
            foreach (Account a in list) { try { await work(a); } catch (Exception ex) { Log.Add(a.Label + ": " + ex.Message); } await Task.Delay(250); }
        }

        static Task CheckAll(App app, List<Account> l) { return Each(l, delegate(Account a) { return app.CheckAsync(a, true); }); }

        static string Summary(List<Account> l) { return string.Join("\n", l.Select(a => a.Label + ": " + StatusText(a)).ToArray()); }

        // ---------------------------------------------------------------- launching
        static Target TargetFrom(Opts o, out bool explicitTarget)
        {
            explicitTarget = true;
            long place = Roblox.ParsePlace(o.Get("place", ""));
            if (o.Has("private"))
            {
                string link = o.Get("private", "");
                string code = Roblox.ParseLinkCode(link);
                long p = Roblox.ParsePlace(link);
                if (code == null) { SavedServer s = Store.Servers.FirstOrDefault(x => x.Name.Equals(link, StringComparison.OrdinalIgnoreCase)); if (s != null) { code = s.Code; p = s.Place; } }
                if (code == null || (p <= 0 && place <= 0)) throw new InvalidOperationException("--private needs a private server link or the name of a saved server.");
                return new Target { Mode = "Private", Place = place > 0 ? place : p, LinkCode = code };
            }
            if (o.Has("job")) { if (place <= 0) throw new InvalidOperationException("--job needs --place too."); return new Target { Mode = "Server", Place = place, Job = o.Get("job", "") }; }
            if (place > 0) return new Target { Mode = "Game", Place = place };
            explicitTarget = false;
            return TheApp().BuildTarget();
        }

        static async Task LaunchTask(App app, List<Account> list, Target t, bool forced, string follow, bool? atOnce, int stagger, Action<string> fail)
        {
            bool oldOnce = Store.Cfg.LaunchAtOnce; int oldStagger = Store.Cfg.StaggerMs;
            try
            {
                if (follow != null)
                {
                    Target ft = await app.FollowTarget(follow);
                    if (ft == null) { fail("Could not find " + follow + " in a game."); return; }
                    t = ft; forced = true;
                }
                if (atOnce.HasValue) Store.Cfg.LaunchAtOnce = atOnce.Value;
                if (stagger >= 0) Store.Cfg.StaggerMs = stagger;
                Engine.ForceShared = forced;
                await Engine.LaunchMany(list, t);
            }
            catch (Exception ex) { fail(ex.Message); }
            finally { Engine.ForceShared = false; Store.Cfg.LaunchAtOnce = oldOnce; Store.Cfg.StaggerMs = oldStagger; }
        }

        static void RegisterLaunch()
        {
            Add("launch", "launch [NAME...|--all|--group G|--tag T] [--place ID|LINK] [--job ID] [--private LINK|NAME] [--follow USER] [--at-once|--sequential] [--stagger MS] [--wait]",
                "Start accounts. Without a game option each account uses its own server, or the game on the Launch page.", delegate(Opts o)
            {
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                if (Engine.Busy) return Err("A launch is already running.");
                App app = TheApp();
                bool explicitTarget;
                Target t = o.Has("follow") ? new Target() : TargetFrom(o, out explicitTarget);
                explicitTarget = o.Has("place") || o.Has("job") || o.Has("private") || o.Has("follow");
                foreach (Account a in l) Engine.BreakerClear(a);
                string failure = null;
                bool? once = o.Has("at-once") ? true : (o.Has("sequential") ? (bool?)false : null);
                Task task = LaunchTask(app, l, t, explicitTarget, o.Has("follow") ? o.Get("follow", "") : null, once, o.Int("stagger", -1), delegate(string m) { failure = m; });
                if (!o.Has("wait")) return Ok("Launching " + l.Count + " account(s): " + string.Join(", ", l.Select(a => a.Label).ToArray()));
                return new Res
                {
                    Done = delegate { return task.IsCompleted; },
                    After = delegate
                    {
                        if (failure != null) return failure;
                        int up = l.Count(a => Engine.Of(a) != null);
                        return up + " of " + l.Count + " running.";
                    },
                    TimeoutMs = 600000
                };
            });
            Add("stop", "stop [NAME...|--all|--group G|--tag T]", "Close the Roblox windows of these accounts.", delegate(Opts o)
            {
                if (o.Has("all")) { int n = Engine.Live.Count; Engine.StopAll(); return Ok("Stopped " + n + "."); }
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                int c = 0;
                foreach (Account a in l) { Running r = Engine.Of(a); if (r != null) { Engine.Kill(r); c++; } }
                return Ok("Stopped " + c + ".");
            });
            Add("restart", "restart NAME...", "Close and start these accounts again in the same game.", delegate(Opts o)
            {
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                App app = TheApp();
                List<Account> go = new List<Account>();
                Target spec = null;
                foreach (Account a in l) { Running r = Engine.Of(a); if (r != null) { spec = r.Spec; Engine.Kill(r); } go.Add(a); }
                Task task = LaunchTask(app, go, spec ?? app.BuildTarget(), spec != null, null, null, -1, delegate(string m) { });
                return new Res { Out = "Restarting " + go.Count + "...", Done = delegate { return task.IsCompleted; }, After = delegate { return go.Count(a => Engine.Of(a) != null) + " of " + go.Count + " running."; }, TimeoutMs = 600000 };
            });
            Add("running", "running [--json]", "The Roblox windows Fleet has started.", delegate(Opts o)
            {
                return Table(o, new string[] { "account", "pid", "uptime", "cpu", "ram", "game" }, Engine.Live.Select(r => Row("account", r.Acc.Label, "pid", r.Pid, "uptime", ((int)(DateTime.Now - r.Started).TotalMinutes) + " min", "cpu", r.Cpu.ToString("0.0") + "%", "ram", (r.Ram >> 20) + " MB", "game", r.Spec.Mode + " " + r.Spec.Place)).ToList());
            });
            Add("layout arrange", "layout arrange", "Put the Roblox windows in their places now.", delegate(Opts o) { int n = Engine.ArrangeNow(false); return Ok(n == 0 ? "No Roblox window to arrange." : "Arranged " + n + " window(s)."); });
            Add("layout status", "layout status", "Screens, the plan and Roblox's smallest size.", delegate(Opts o)
            {
                List<System.Windows.Forms.Screen> sc = Engine.TargetScreens();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Screens used: " + sc.Count + (Store.Cfg.LayoutAllScreens ? " (all)" : ""));
                foreach (System.Windows.Forms.Screen s in sc) sb.AppendLine("  " + s.DeviceName + "  " + s.WorkingArea.Width + "x" + s.WorkingArea.Height + (s.Primary ? "  primary" : ""));
                sb.AppendLine("Windows running: " + Engine.Live.Count);
                sb.AppendLine("Roblox smallest size seen: " + (Engine.LearnedMin.Width > 0 ? Engine.LearnedMin.Width + " x " + Engine.LearnedMin.Height : "none (no limit hit)"));
                return Ok(sb.ToString().TrimEnd());
            });
            Add("layout reset-min", "layout reset-min", "Forget the smallest size Roblox refused.", delegate(Opts o) { Engine.LearnedMin = System.Drawing.Size.Empty; return Ok("ok"); });
        }

        // ---------------------------------------------------------------- saved servers, presets, health
        static SavedServer FindServer(string name)
        {
            return Store.Servers.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? Store.Servers.FirstOrDefault(s => s.Id == name);
        }

        static void RegisterServers()
        {
            Add("server list", "server list [--json]", "Saved private servers.", delegate(Opts o)
            {
                return Table(o, new string[] { "name", "place", "check", "accounts" }, Store.Servers.Select(s => Row("name", s.Name, "place", s.Place, "check", s.CheckText, "accounts", Store.Accounts.Count(a => a.ServerId == s.Id))).ToList());
            });
            Add("server add", "server add NAME LINK [--place ID]", "Save a private server link.", delegate(Opts o)
            {
                if (o.Args.Count < 2) return Err("Give a name and the private server link.");
                string link = o.Args[1];
                string code = Roblox.ParseLinkCode(link);
                long place = Roblox.ParsePlace(link); if (o.Has("place")) place = Roblox.ParsePlace(o.Get("place", ""));
                if (code == null || place <= 0) return Err("That is not a private server link (it needs ?privateServerLinkCode=...). The place can be given with --place.");
                Store.Servers.Add(new SavedServer { Name = o.Args[0], Place = place, Code = code, Link = link });
                Store.SaveAccounts();
                return Ok("Saved " + o.Args[0] + ".");
            });
            Add("server remove", "server remove NAME", "Delete a saved server.", delegate(Opts o)
            {
                SavedServer s = o.Args.Count > 0 ? FindServer(o.Args[0]) : null;
                if (s == null) return Err("No saved server with that name.");
                foreach (Account a in Store.Accounts.Where(a => a.ServerId == s.Id)) a.ServerId = "";
                Store.Servers.Remove(s); Store.SaveAccounts();
                return Ok("Removed.");
            });
            Add("server assign", "server assign SERVER ACCOUNT...", "Give these accounts that server.", delegate(Opts o)
            {
                if (o.Args.Count < 2) return Err("Give the server and the accounts.");
                SavedServer s = FindServer(o.Args[0]);
                if (s == null) return Err("No saved server called " + o.Args[0] + ".");
                o.Args.RemoveAt(0);
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                foreach (Account a in l) a.ServerId = s.Id;
                Store.SaveAccounts();
                return Ok("Assigned " + l.Count + " account(s) to " + s.Name + ".");
            });
            Add("server unassign", "server unassign ACCOUNT...", "Take their server away.", delegate(Opts o)
            {
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                foreach (Account a in l) a.ServerId = "";
                Store.SaveAccounts();
                return Ok("ok");
            });
            Add("server check", "server check [NAME] [--account A]", "Ask Roblox whether the saved links can still be joined.", delegate(Opts o)
            {
                List<SavedServer> list = o.Args.Count > 0 ? Store.Servers.Where(s => s.Name.Equals(o.Args[0], StringComparison.OrdinalIgnoreCase)).ToList() : Store.Servers.ToList();
                if (list.Count == 0) return Err("No saved server matched.");
                Account who = o.Has("account") ? Match(o.Get("account", ""), Store.Accounts).FirstOrDefault() : Store.Accounts.FirstOrDefault(a => a.Status == 1) ?? Store.Accounts.FirstOrDefault();
                if (who == null) return Err("Needs an account to ask with.");
                Task t = Each2(list, async delegate(SavedServer s)
                {
                    try { string r = await Roblox.CheckPrivate(who, s.Place, s.Code); s.Check = r == "OK" ? 1 : 2; s.CheckText = r == "OK" ? "Joinable" : r; }
                    catch (Exception) { s.Check = 2; s.CheckText = "Could not check"; }
                });
                return new Res { Done = delegate { return t.IsCompleted; }, After = delegate { Store.SaveAccounts(); return string.Join("\n", list.Select(s => s.Name + ": " + s.CheckText).ToArray()); } };
            });
            Add("server public", "server public PLACE [--limit N]", "Public servers of a game: players and ping.", delegate(Opts o)
            {
                long place = o.Args.Count > 0 ? Roblox.ParsePlace(o.Args[0]) : 0;
                if (place <= 0) return Err("Give a place id or game link.");
                Task<List<Roblox.PublicServer>> t = Roblox.Servers(place);
                return new Res { Done = delegate { return t.IsCompleted; }, After = delegate
                {
                    if (t.IsFaulted) return "Could not load: " + t.Exception.GetBaseException().Message;
                    int lim = o.Int("limit", 20);
                    return Table(o, new string[] { "id", "players", "ping", "fps" }, t.Result.Take(lim).Select(s => Row("id", s.Id, "players", s.Playing + "/" + s.Max, "ping", s.Ping, "fps", s.Fps.ToString("0"))).ToList()).Out;
                } };
            });

            Add("preset list", "preset list [--json]", "Saved presets.", delegate(Opts o)
            {
                return Table(o, new string[] { "name", "accounts", "mode", "place" }, Store.Presets.Select(p => Row("name", p.Name, "accounts", p.AccountIds.Count, "mode", p.Mode, "place", p.Place)).ToList());
            });
            Add("preset run", "preset run NAME", "Launch a preset (accounts, game, then arrange).", delegate(Opts o)
            {
                Preset p = o.Args.Count > 0 ? Store.Presets.FirstOrDefault(x => x.Name.Equals(o.Args[0], StringComparison.OrdinalIgnoreCase)) : null;
                if (p == null) return Err("No preset with that name.");
                TheApp().RunPreset(p);
                return Ok("Running preset " + p.Name + ".");
            });
            Add("preset save", "preset save NAME --accounts a,b [--place ID] [--private LINK] [--job ID]", "Save a preset.", delegate(Opts o)
            {
                if (o.Args.Count == 0) return Err("Give a name.");
                string name = o.Args[0]; o.Args.RemoveAt(0);
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                Preset p = new Preset { Name = name, AccountIds = l.Select(a => a.Id).ToList(), Mode = o.Has("private") ? "Private" : (o.Has("job") ? "Server" : "Game"), Place = o.Get("place", Store.Cfg.LastPlace), Job = o.Get("job", ""), Private = o.Get("private", "") };
                Store.Presets.RemoveAll(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                Store.Presets.Add(p); Store.SavePresets();
                return Ok("Saved " + name + " with " + l.Count + " account(s).");
            });
            Add("preset delete", "preset delete NAME", "Delete a preset.", delegate(Opts o)
            {
                int n = o.Args.Count > 0 ? Store.Presets.RemoveAll(x => x.Name.Equals(o.Args[0], StringComparison.OrdinalIgnoreCase)) : 0;
                Store.SavePresets();
                return n > 0 ? Ok("Deleted.") : Err("No preset with that name.");
            });

            Add("health check", "health check [NAME...|--all]", "Check the logins (same as account check, always waits).", delegate(Opts o)
            {
                if (o.Args.Count == 0 && !o.Has("group") && !o.Has("tag")) o.Flags["all"] = "true";
                List<Account> l; Res bad = NeedAccounts(o, out l); if (bad != null) return bad;
                Task t = CheckAll(TheApp(), l);
                return new Res { Out = "Checking " + l.Count + " login(s)...", Done = delegate { return t.IsCompleted; }, After = delegate { return Summary(l); } };
            });
            Add("health journal", "health journal [--account A] [--kind K] [--count N]", "Crashes, kicks, relaunches and logins that stopped working.", delegate(Opts o)
            {
                IEnumerable<JournalEntry> q = Store.Journal;
                if (o.Has("account")) { string a = o.Get("account", ""); q = q.Where(e => e.Account.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0); }
                if (o.Has("kind")) { string k = o.Get("kind", ""); q = q.Where(e => e.Kind.Equals(k, StringComparison.OrdinalIgnoreCase)); }
                List<JournalEntry> l = q.Skip(Math.Max(0, q.Count() - o.Int("count", 30))).ToList();
                return Table(o, new string[] { "time", "account", "kind", "text" }, l.Select(e => Row("time", new DateTime(e.Ticks, DateTimeKind.Utc).ToLocalTime().ToString("MM-dd HH:mm"), "account", e.Account, "kind", e.Kind, "text", e.Text)).ToList());
            });
            Add("health export", "health export FILE", "Write the journal to a csv file.", delegate(Opts o)
            {
                if (o.Args.Count == 0) return Err("Give the file.");
                Journal2.ExportCsv(o.Args[0]);
                return Ok("Wrote " + o.Args[0] + ".");
            });
        }

        static async Task Each2<T>(List<T> list, Func<T, Task> work)
        {
            foreach (T x in list) { try { await work(x); } catch (Exception) { } await Task.Delay(200); }
        }
    }
}
