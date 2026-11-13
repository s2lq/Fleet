using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fleet
{
    public static partial class Cli
    {
        static readonly string[] DayWords = { "mon", "tue", "wed", "thu", "fri", "sat", "sun" };

        static int ParseDays(string text)
        {
            string t = (text ?? "").Trim().ToLowerInvariant();
            if (t == "" || t == "every" || t == "daily" || t == "all") return 127;
            if (t == "weekdays") return 31;
            if (t == "weekends") return 96;
            int mask = 0;
            foreach (string d in t.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int i = Array.IndexOf(DayWords, d.Length >= 3 ? d.Substring(0, 3) : d);
                if (i < 0) return -1;
                mask |= 1 << i;
            }
            return mask;
        }

        static void RegisterData()
        {
            Add("roblox where", "roblox where", "The Roblox installs Fleet found.", delegate(Opts o)
            {
                List<string> exes = Engine.RobloxExes();
                if (exes.Count == 0) return Err("Roblox was not found. Set the folder with: roblox folder PATH");
                return Ok(string.Join("\n", exes.ToArray()));
            });
            Add("roblox folder", "roblox folder PATH|auto", "Tell Fleet where Roblox's Versions folder is (auto goes back to searching).", delegate(Opts o)
            {
                string p = o.Rest(0);
                if (p.Length > 0 && p != "auto" && !Directory.Exists(p)) return Err("No such folder: " + p);
                Store.Cfg.RobloxFolder = p == "auto" ? "" : p; AfterSet("RobloxFolder");
                return Ok(Engine.FindRoblox() ?? "Roblox was not found there.");
            });
            Add("roblox versions", "roblox versions [--releases]", "Versions Fleet downloaded, or with --releases the recent ones on Roblox's servers.", delegate(Opts o)
            {
                if (!o.Has("releases"))
                    return Table(o, new string[] { "version", "size", "default" }, Versions.Installed().Select(v => Row("version", v, "size", (Versions.SizeOf(v) >> 20) + " MB", "default", v == Store.Cfg.RobloxVersion ? "yes" : "")).ToList());
                Task<List<Versions.Release>> t = Versions.Recent();
                return new Res { Done = delegate { return t.IsCompleted; }, After = delegate
                {
                    if (t.IsFaulted) return "Could not load: " + t.Exception.GetBaseException().Message;
                    return Table(o, new string[] { "version", "date" }, t.Result.Select(r => Row("version", r.Version, "date", r.Date)).ToList()).Out;
                } };
            });
            Add("roblox download", "roblox download VERSION|newest", "Download a Roblox version into Fleet's own folder.", delegate(Opts o)
            {
                string v = o.Rest(0);
                if (v.Length == 0) return Err("Give a version-... name or newest.");
                Task<string> t = DownloadVersion(v);
                return new Res { Done = delegate { return t.IsCompleted; }, After = delegate { return t.IsFaulted ? "Failed: " + t.Exception.GetBaseException().Message : (t.Result ?? "Downloaded."); }, TimeoutMs = 1800000 };
            });
            Add("roblox progress", "roblox progress", "Progress of a running download.", delegate(Opts o)
            {
                return Ok(Versions.Current.Length == 0 ? "No download running." : "Downloading " + Versions.Current + ": " + (int)(Versions.Progress * 100) + " %");
            });
            Add("roblox delete", "roblox delete VERSION", "Delete a downloaded version.", delegate(Opts o)
            {
                string err = Versions.Delete(o.Rest(0));
                return err == null ? Ok("Deleted.") : Err(err);
            });
            Add("roblox use", "roblox use VERSION|default", "Which downloaded version launches use (default is Roblox's own).", delegate(Opts o)
            {
                string v = o.Rest(0);
                if (v == "default" || v == "") Store.Cfg.RobloxVersion = "";
                else if (!Versions.Installed().Contains(v)) return Err("That version is not downloaded. See: roblox versions");
                else Store.Cfg.RobloxVersion = v;
                Store.SaveSettings();
                return Ok("Launches use " + (Store.Cfg.RobloxVersion.Length == 0 ? "Roblox's own version" : Store.Cfg.RobloxVersion) + ".");
            });

            Add("schedule list", "schedule list", "Scheduled actions.", delegate(Opts o)
            {
                int i = 0;
                return Table(o, new string[] { "#", "name", "time", "days", "action", "preset", "on" }, Store.Cfg.Schedule.Select(s => Row("#", i++, "name", s.Name, "time", s.Time, "days", DaysWords(s.Days), "action", s.Action, "preset", s.Preset, "on", s.Enabled ? "yes" : "no")).ToList());
            });
            Add("schedule add", "schedule add --time HH:mm [--days mon,tue|weekdays|weekends] --action \"Launch preset|Launch all|Stop all\" [--preset NAME] [--name N]", "Add a scheduled action.", delegate(Opts o)
            {
                string time = o.Get("time", "");
                DateTime d;
                if (!DateTime.TryParseExact(time, "HH:mm", null, System.Globalization.DateTimeStyles.None, out d)) return Err("Give --time as HH:mm, for example 20:30.");
                int days = ParseDays(o.Get("days", "every"));
                if (days < 0) return Err("Days: mon,tue,... or weekdays, weekends, every.");
                string action = new string[] { "Launch preset", "Launch all", "Stop all" }.FirstOrDefault(a => a.Equals(o.Get("action", "Launch preset"), StringComparison.OrdinalIgnoreCase));
                if (action == null) return Err("Action: Launch preset, Launch all or Stop all.");
                string preset = o.Get("preset", "");
                if (action == "Launch preset" && !Store.Presets.Any(p => p.Name.Equals(preset, StringComparison.OrdinalIgnoreCase))) return Err("Give --preset with a saved preset name.");
                Store.Cfg.Schedule.Add(new ScheduleItem { Name = o.Get("name", ""), Time = time, Days = days, Action = action, Preset = preset });
                Store.SaveSettings();
                return Ok("Added.");
            });
            Add("schedule remove", "schedule remove INDEX", "Remove one (see schedule list).", delegate(Opts o)
            {
                int i; if (o.Args.Count == 0 || !int.TryParse(o.Args[0], out i) || i < 0 || i >= Store.Cfg.Schedule.Count) return Err("Give the number from schedule list.");
                Store.Cfg.Schedule.RemoveAt(i); Store.SaveSettings();
                return Ok("Removed.");
            });
            Add("schedule enable", "schedule enable INDEX [--disable]", "Turn one on or off.", delegate(Opts o)
            {
                int i; if (o.Args.Count == 0 || !int.TryParse(o.Args[0], out i) || i < 0 || i >= Store.Cfg.Schedule.Count) return Err("Give the number from schedule list.");
                Store.Cfg.Schedule[i].Enabled = !o.Has("disable"); Store.SaveSettings();
                return Ok(Store.Cfg.Schedule[i].Enabled ? "On." : "Off.");
            });

            Add("macro status", "macro status", "The macro recorder.", delegate(Opts o) { return Ok((Macro.Recording ? "recording" : (Macro.Playing ? "playing" : "idle")) + ", " + Macro.Steps.Count + " step(s)"); });
            Add("macro record", "macro record", "Start recording the mouse and keyboard.", delegate(Opts o) { Macro.Record(); return Ok(Macro.Recording ? "Recording. Stop with: macro stop" : "Could not start."); });
            Add("macro stop", "macro stop", "Stop recording or playing.", delegate(Opts o) { if (Macro.Recording) Macro.StopRecord(); Macro.StopPlay(); return Ok(Macro.Steps.Count + " step(s)."); });
            Add("macro play", "macro play [--loops N]", "Play the recording.", delegate(Opts o) { Macro.Play(Math.Max(1, o.Int("loops", Store.Cfg.MacroLoops))); return Ok("Playing."); });
            Add("macro save", "macro save FILE", "Save the recording.", delegate(Opts o) { if (o.Args.Count == 0) return Err("Give the file."); Macro.Save(o.Args[0]); return Ok("Saved."); });
            Add("macro load", "macro load FILE", "Load a recording.", delegate(Opts o) { if (o.Args.Count == 0 || !File.Exists(o.Args[0])) return Err("No such file."); Macro.Load(o.Args[0]); return Ok(Macro.Steps.Count + " step(s)."); });

            Add("backup now", "backup now", "Make a backup copy of the accounts file.", delegate(Opts o) { string f = Store.Backup(); return f == null ? Err("Nothing to back up.") : Ok(f); });
            Add("backup list", "backup list", "The backups Fleet keeps.", delegate(Opts o)
            {
                string dir = Path.Combine(Store.Dir, "Backups");
                if (!Directory.Exists(dir)) return Ok("(none)");
                return Ok(string.Join("\n", Directory.GetFiles(dir, "accounts-*.fdat").OrderByDescending(x => x).Select(f => f + "   " + (new FileInfo(f).Length / 1024) + " KB").ToArray()));
            });
            Add("backup restore", "backup restore FILE --yes", "Replace the accounts with a backup (the current ones are backed up first).", delegate(Opts o)
            {
                if (o.Args.Count == 0 || !File.Exists(o.Args[0])) return Err("Give the backup file.");
                if (!o.Has("yes")) return Err("This replaces your accounts. Add --yes.");
                string err = Store.RestoreFrom(o.Args[0]);
                return err == null ? Ok("Restored " + Store.Accounts.Count + " account(s).") : Err(err);
            });
            Add("data path", "data path", "Where Fleet keeps its data.", delegate(Opts o) { return Ok(Store.Dir + (Store.IsPortable ? "   (portable)" : "")); });

            Add("proxy test", "proxy test [PROXY]", "Time a proxy (or the first one in the pool).", delegate(Opts o)
            {
                string p = o.Rest(0); if (p.Length == 0) p = Proxies.Pool().FirstOrDefault() ?? "";
                if (p.Length == 0) return Err("Give a proxy, or fill the proxy pool first.");
                Task<int> t = Proxies.Test(p);
                return new Res { Done = delegate { return t.IsCompleted; }, After = delegate { return t.Result < 0 ? "Did not answer." : t.Result + " ms"; } };
            });
            Add("proxy assign", "proxy assign", "Hand the pool out over all accounts.", delegate(Opts o) { return Ok("Assigned " + Proxies.AssignAll() + "."); });
            Add("proxy clear", "proxy clear", "Take back the proxies that were handed out automatically.", delegate(Opts o) { return Ok("Cleared " + Proxies.RemoveAuto() + "."); });
            Add("webhook test", "webhook test", "Send a test message to the Discord webhook.", delegate(Opts o)
            {
                if (!Store.Cfg.Webhook.StartsWith("https://")) return Err("No webhook set. Use: config set Webhook https://discord.com/api/webhooks/...");
                Hook.Send("Fleet", "This is a test from the command line.", Hook.Green);
                return Ok("Sent.");
            });

            Add("gallery list", "gallery list", "Configs and themes shared by others.", delegate(Opts o)
            {
                Task<List<string[]>> t = TheApp().GalleryRows();
                return new Res { Done = delegate { return t.IsCompleted; }, After = delegate
                {
                    if (t.IsFaulted) return "Could not load the gallery: " + t.Exception.GetBaseException().Message;
                    return Table(o, new string[] { "type", "name", "by", "about" }, t.Result.Select(r => Row("type", r[0], "name", r[1], "by", r[2], "about", r[3])).ToList()).Out;
                } };
            });
            Add("gallery install", "gallery install NAME", "Install a config or theme from the gallery.", delegate(Opts o)
            {
                Task<string> t = TheApp().GalleryInstallByName(o.Rest(0));
                return new Res { Done = delegate { return t.IsCompleted; }, After = delegate { return t.IsFaulted ? "Failed: " + t.Exception.GetBaseException().Message : (t.Result ?? "Nothing in the gallery is called " + o.Rest(0) + "."); } };
            });

            Add("update check", "update check", "Is a newer Fleet out?", delegate(Opts o)
            {
                Task<bool> t = Updater.Check();
                return new Res { Done = delegate { return t.IsCompleted; }, After = delegate { return t.IsFaulted ? "Could not check." : (t.Result ? "Fleet " + Updater.Latest + " is available (you have " + Program.Version + ")." : "You are up to date (" + Program.Version + ")."); } };
            });
            Add("update install", "update install", "Download the newest Fleet and restart into it.", delegate(Opts o)
            {
                Task<string> t = Updater.Install();
                return new Res { Done = delegate { return t.IsCompleted; }, After = delegate
                {
                    if (t.IsFaulted) return "Failed: " + t.Exception.GetBaseException().Message;
                    if (t.Result != null) return t.Result;
                    MainForm f = Form(); if (f != null) f.BeginInvoke((System.Windows.Forms.MethodInvoker)delegate { f.Quit(); });
                    return "Installed. Fleet is restarting.";
                }, TimeoutMs = 600000 };
            });
        }

        static string DaysWords(int days)
        {
            if (days == 127) return "every day";
            if (days == 31) return "weekdays";
            if (days == 96) return "weekends";
            return string.Join(",", Enumerable.Range(0, 7).Where(i => ((days >> i) & 1) == 1).Select(i => DayWords[i]).ToArray());
        }

        static async Task<string> DownloadVersion(string v)
        {
            if (v == "newest")
            {
                List<Versions.Release> r = await Versions.Recent();
                if (r.Count == 0) return "Could not find the newest version.";
                v = r[0].Version;
            }
            return await Versions.Download(v, delegate(string s) { });
        }
    }
}
