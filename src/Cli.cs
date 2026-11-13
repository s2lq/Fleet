using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Fleet
{
    // The command line. fleetctl.exe (or Fleet.exe with a command) sends the words to the running Fleet over a pipe,
    // Fleet runs the command and sends the text back. Everything the window can do is a command here.
    public class Res
    {
        public string Out = "";
        public int Code;
        public Func<bool> Done;         // the command started something slow: keep asking until this is true
        public Func<string> After;      // what to print once it is
        public int TimeoutMs = 180000;
    }

    public class Opts
    {
        public List<string> Args = new List<string>();
        public Dictionary<string, string> Flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string Stdin = "";
        public bool Has(string k) { string v; return Flags.TryGetValue(k, out v) && v != "false"; }
        public string Get(string k, string d) { string v; return Flags.TryGetValue(k, out v) ? v : d; }
        public int Int(string k, int d) { int n; return int.TryParse(Get(k, ""), out n) ? n : d; }
        public string Rest(int from) { return string.Join(" ", Args.Skip(from).ToArray()); }
    }

    public delegate Res CliHandler(Opts o);

    public static partial class Cli
    {
        class Command { public string Name, Usage, Summary; public CliHandler Run; }
        static readonly List<Command> commands = new List<Command>();
        static readonly JavaScriptSerializer js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        // options that never take a value
        static readonly HashSet<string> BoolFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "json", "all", "yes", "wait", "cookies", "running", "desc", "releases", "quiet", "force", "stdin", "secrets", "at-once", "sequential", "help", "enable", "disable", "signed-out", "arrange"
        };

        static void Add(string name, string usage, string summary, CliHandler run) { commands.Add(new Command { Name = name, Usage = usage, Summary = summary, Run = run }); }

        static Cli()
        {
            RegisterCore();
            RegisterAccounts();
            RegisterLaunch();
            RegisterServers();
            RegisterSystem();
            RegisterData();
        }

        // ---------------------------------------------------------------- results
        public static Res Ok(string text) { return new Res { Out = text }; }
        public static Res Err(string text) { return new Res { Out = text, Code = 1 }; }
        static Res Json(object o) { return new Res { Out = js.Serialize(o) }; }

        // a list as a table, or as json with --json
        static Res Table(Opts o, string[] cols, List<Dictionary<string, object>> rows)
        {
            if (o.Has("json")) return Json(rows);
            if (rows.Count == 0) return Ok("(nothing)");
            int[] w = cols.Select(c => c.Length).ToArray();
            List<string[]> cells = new List<string[]>();
            foreach (Dictionary<string, object> r in rows)
            {
                string[] line = cols.Select(c => Clean(r.ContainsKey(c) ? Convert.ToString(r[c]) : "")).ToArray();
                for (int i = 0; i < cols.Length; i++) w[i] = Math.Max(w[i], line[i].Length);
                cells.Add(line);
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Join("  ", cols.Select((c, i) => c.ToUpper().PadRight(w[i])).ToArray()).TrimEnd());
            foreach (string[] line in cells) sb.AppendLine(string.Join("  ", line.Select((c, i) => c.PadRight(w[i])).ToArray()).TrimEnd());
            return Ok(sb.ToString().TrimEnd());
        }

        static string Clean(string s) { return (s ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " "); }

        static Dictionary<string, object> Row(params object[] kv)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        // ---------------------------------------------------------------- handles on the running app
        static MainForm Form() { return Application.OpenForms.OfType<MainForm>().FirstOrDefault(); }
        static App TheApp() { MainForm f = Form(); return f == null ? null : f.View.AppRef; }

        // ---------------------------------------------------------------- parsing and running
        public static Opts Parse(string[] argv)
        {
            Opts o = new Opts();
            for (int i = 0; i < argv.Length; i++)
            {
                string a = argv[i];
                if (a == "-h" || a == "/?") { o.Flags["help"] = "true"; continue; }
                if (a.StartsWith("--") && a.Length > 2)
                {
                    string k = a.Substring(2), v = null;
                    int eq = k.IndexOf('=');
                    if (eq > 0) { v = k.Substring(eq + 1); k = k.Substring(0, eq); }
                    else if (!BoolFlags.Contains(k) && i + 1 < argv.Length && !argv[i + 1].StartsWith("--")) v = argv[++i];
                    o.Flags[k] = v ?? "true";
                }
                else o.Args.Add(a);
            }
            return o;
        }

        // finds the command for the first one or two words. The words are taken off Args.
        static Command Find(Opts o)
        {
            for (int n = Math.Min(2, o.Args.Count); n >= 1; n--)
            {
                string name = string.Join(" ", o.Args.Take(n).Select(x => x.ToLowerInvariant()).ToArray());
                Command c = commands.FirstOrDefault(x => x.Name == name);
                if (c != null) { o.Args.RemoveRange(0, n); return c; }
            }
            return null;
        }

        public static Res Dispatch(string[] argv, string stdin)
        {
            Opts o = Parse(argv);
            o.Stdin = stdin ?? "";
            if (o.Args.Count == 0) return Help(o);
            if (o.Args[0].Equals("help", StringComparison.OrdinalIgnoreCase)) { o.Args.RemoveAt(0); return Help(o); }
            Command c = Find(o);
            if (c == null)
            {
                string w = o.Args[0].ToLowerInvariant();
                List<string> group = commands.Where(x => x.Name.StartsWith(w + " ")).Select(x => x.Name).ToList();
                if (group.Count > 0) return Err("Which one? " + string.Join(", ", group.ToArray()) + "\nTry: help " + w);
                return Err("Unknown command: " + o.Args[0] + ". Try: help");
            }
            if (o.Has("help")) return Ok(c.Name + "\n  " + c.Usage + "\n  " + c.Summary);
            try { return c.Run(o); }
            catch (Exception ex) { return Err("Error: " + ex.Message); }
        }

        static Res Help(Opts o)
        {
            if (o.Args.Count > 0)
            {
                string q = string.Join(" ", o.Args.ToArray()).ToLowerInvariant();
                List<Command> hit = commands.Where(c => c.Name == q || c.Name.StartsWith(q + " ")).ToList();
                if (hit.Count == 0) return Err("No command called " + q + ".");
                return Ok(string.Join("\n\n", hit.Select(c => c.Usage + "\n    " + c.Summary).ToArray()));
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("fleetctl <command> [options]     (add --json for machine readable output)");
            sb.AppendLine("Use  help <command>  for details, for example  help account  or  help launch.");
            string last = "";
            foreach (Command c in commands)
            {
                string g = c.Name.Contains(" ") ? c.Name.Split(' ')[0] : "";
                if (g != last) { sb.AppendLine(); last = g; }
                sb.AppendLine("  " + c.Name.PadRight(24) + c.Summary);
            }
            return Ok(sb.ToString().TrimEnd());
        }

        // ---------------------------------------------------------------- the pipe
        public const string PipeName = "Fleet_Cli";
        public const char Sep = '\u001f';
        public const string ExitMark = "\u0001EXIT ";

        public static void Listen(MainForm ui)
        {
            Thread t = new Thread(delegate()
            {
                while (true)
                {
                    try
                    {
                        NamedPipeServerStream s = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 8, PipeTransmissionMode.Byte);
                        s.WaitForConnection();
                        Thread w = new Thread(delegate() { Serve(ui, s); });
                        w.IsBackground = true;
                        w.Start();
                    }
                    catch (Exception) { Thread.Sleep(500); }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        static void Serve(MainForm ui, NamedPipeServerStream s)
        {
            try
            {
                using (s)
                {
                    UTF8Encoding enc = new UTF8Encoding(false);
                    StreamReader r = new StreamReader(s, enc);
                    StreamWriter w = new StreamWriter(s, enc) { AutoFlush = true, NewLine = "\n" };
                    string line = r.ReadLine(), body = r.ReadLine();
                    if (line == null) return;
                    string[] argv = line.Length == 0 ? new string[0] : line.Split(Sep);
                    string stdin = "";
                    try { if (!string.IsNullOrEmpty(body)) stdin = enc.GetString(Convert.FromBase64String(body)); } catch (Exception) { }
                    Res res;
                    try { res = (Res)ui.Invoke((Func<Res>)delegate { return Dispatch(argv, stdin); }); }
                    catch (Exception ex) { res = Err("Error: " + ex.Message); }
                    string text = res.Out;
                    if (res.Done != null)
                    {
                        DateTime end = DateTime.UtcNow.AddMilliseconds(res.TimeoutMs);
                        bool done = false;
                        while (DateTime.UtcNow < end)
                        {
                            try { done = (bool)ui.Invoke((Func<bool>)delegate { return res.Done(); }); } catch (Exception) { done = true; }
                            if (done) break;
                            Thread.Sleep(250);
                        }
                        if (!done) { res.Code = 2; text += (text.Length > 0 ? "\n" : "") + "Still working after " + res.TimeoutMs / 1000 + " s, not waiting any longer."; }
                        else if (res.After != null)
                        {
                            try { string more = (string)ui.Invoke((Func<string>)delegate { return res.After(); }); if (!string.IsNullOrEmpty(more)) text += (text.Length > 0 ? "\n" : "") + more; } catch (Exception) { }
                        }
                    }
                    w.Write(text.Replace("\r", ""));
                    w.Write("\n" + ExitMark + res.Code + "\n");
                }
            }
            catch (Exception) { }
        }

        // ---------------------------------------------------------------- the first few commands
        static void RegisterCore()
        {
            Add("status", "status", "What Fleet is doing right now.", delegate(Opts o)
            {
                App app = TheApp();
                string roblox = Engine.FindRoblox() ?? "";
                Dictionary<string, object> d = Row("version", Program.Version, "accounts", Store.Accounts.Count, "running", Engine.Live.Count, "busy", Engine.Busy, "multiInstance", Engine.MultiActive,
                    "roblox", roblox, "dataDir", Store.Dir, "portable", Store.IsPortable, "autoRelaunch", Store.Cfg.AutoRelaunch, "autoArrange", Store.Cfg.AutoArrange, "theme", Store.Cfg.Theme);
                if (o.Has("json")) return Json(d);
                return Ok("Fleet " + Program.Version + "\nAccounts: " + Store.Accounts.Count + ", running: " + Engine.Live.Count + (Engine.Busy ? " (launching: " + Engine.BusyText + ")" : "") +
                    "\nMulti-instance: " + (Engine.MultiActive ? "on" : "off") + "\nRoblox: " + (roblox.Length > 0 ? roblox : "not found") + "\nData: " + Store.Dir);
            });
            Add("version", "version", "Fleet's version.", delegate(Opts o) { return Ok(Program.Version); });
            Add("show", "show", "Bring the Fleet window to the front.", delegate(Opts o) { MainForm f = Form(); if (f != null) f.ShowMain(); return Ok("ok"); });
            Add("hide", "hide", "Hide the Fleet window (it keeps running in the tray).", delegate(Opts o) { MainForm f = Form(); if (f != null) f.Hide(); return Ok("ok"); });
            Add("mini", "mini", "Switch to the small always-on-top window and back.", delegate(Opts o) { MainForm f = Form(); if (f != null) f.ToggleMini(); return Ok("ok"); });
            Add("quit", "quit", "Close Fleet.", delegate(Opts o) { MainForm f = Form(); if (f != null) f.BeginInvoke((MethodInvoker)delegate { f.Quit(); }); return Ok("Closing Fleet."); });
            Add("log", "log [--lines N]", "The activity log (the same lines as the toasts).", delegate(Opts o)
            {
                int n = o.Int("lines", 30);
                List<string> lines;
                lock (Log.Lines) lines = Log.Lines.Skip(Math.Max(0, Log.Lines.Count - n)).Select(e => e.At.ToString("HH:mm:ss") + "  " + e.Text).ToList();
                return Ok(string.Join("\n", lines.ToArray()));
            });
        }
    }
}
