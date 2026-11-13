using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Fleet
{
    public static partial class Cli
    {
        // ---------------------------------------------------------------- settings by name
        static readonly string[] Secret = { "Webhook", "ProxyPool", "DiscordAppId" };

        static bool Simple(Type t) { return t == typeof(bool) || t == typeof(int) || t == typeof(long) || t == typeof(double) || t == typeof(float) || t == typeof(string); }

        static FieldInfo SettingField(string name)
        {
            return typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(f => Simple(f.FieldType) && f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        static object Convert2(string v, Type t)
        {
            if (t == typeof(bool))
            {
                string x = v.Trim().ToLowerInvariant();
                if (x == "true" || x == "on" || x == "yes" || x == "1") return true;
                if (x == "false" || x == "off" || x == "no" || x == "0") return false;
                throw new FormatException("Use on or off.");
            }
            if (t == typeof(int)) return int.Parse(v);
            if (t == typeof(long)) return long.Parse(v);
            if (t == typeof(double)) return double.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
            if (t == typeof(float)) return float.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
            return v;
        }

        // the things that have to happen when a setting changes, the same as when you click it in the window
        static void AfterSet(string key)
        {
            MainForm f = Form();
            switch (key)
            {
                case "Theme": Themes.Apply(Store.Cfg.Theme); break;
                case "Language": Lang.Load(Store.Cfg.Language); break;
                case "MultiInstance": Engine.SetMultiInstance(Store.Cfg.MultiInstance); break;
                case "UrlHandler": Shell.SetUrlHandler(Store.Cfg.UrlHandler); break;
                case "StartWithWindows": Shell.SetStartup(Store.Cfg.StartWithWindows); break;
                case "GfxPreset": Perf.SetGraphics(Store.Cfg.GfxPreset); break;
                case "FpsCap": Engine.SetFpsCap(Store.Cfg.FpsCap); Perf.CapChanged(); break;
                case "Priority": Engine.ApplyPriorityAll(); break;
                case "CpuCapMode": case "CpuCapPercent": case "RamCapMode": case "ThrottleBackground": case "BackgroundFps": Perf.CapChanged(); break;
                case "RobloxFolder": Engine.ForgetRoblox(); break;
            }
            Store.SaveSettings();
            if (f != null) f.ApplyShell();
        }

        static Res ListSettings(Opts o)
        {
            string filter = o.Args.Count > 0 ? o.Args[0] : "";
            List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
            foreach (FieldInfo f in typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!Simple(f.FieldType) || (filter.Length > 0 && f.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                object v = f.GetValue(Store.Cfg);
                string text = Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
                if (v is bool) text = (bool)v ? "on" : "off";
                if (Secret.Contains(f.Name) && !o.Has("secrets") && !string.IsNullOrEmpty(text)) text = "(set)";
                rows.Add(Row("key", f.Name, "value", text, "type", f.FieldType == typeof(bool) ? "on/off" : f.FieldType.Name.ToLowerInvariant()));
            }
            return Table(o, new string[] { "key", "value", "type" }, rows);
        }

        static void RegisterSystem()
        {
            Add("config list", "config list [FILTER] [--json]", "Every setting and its value. Same names as the window's settings.", delegate(Opts o) { return ListSettings(o); });
            Add("config get", "config get KEY", "One setting.", delegate(Opts o)
            {
                FieldInfo f = o.Args.Count > 0 ? SettingField(o.Args[0]) : null;
                if (f == null) return Err("No setting called " + (o.Args.Count > 0 ? o.Args[0] : "") + ". Try: config list");
                object v = f.GetValue(Store.Cfg);
                return Ok(v is bool ? ((bool)v ? "on" : "off") : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));
            });
            Add("config set", "config set KEY VALUE", "Change a setting (on/off, a number or text). Takes effect right away.", delegate(Opts o)
            {
                if (o.Args.Count < 2) return Err("Usage: config set KEY VALUE");
                FieldInfo f = SettingField(o.Args[0]);
                if (f == null) return Err("No setting called " + o.Args[0] + ". Try: config list");
                try { f.SetValue(Store.Cfg, Convert2(o.Rest(1), f.FieldType)); }
                catch (Exception ex) { return Err("That value does not fit " + f.Name + " (" + ex.Message + ")."); }
                AfterSet(f.Name);
                return Ok(f.Name + " = " + Convert.ToString(f.GetValue(Store.Cfg)));
            });
            Add("config toggle", "config toggle KEY", "Flip an on/off setting.", delegate(Opts o)
            {
                FieldInfo f = o.Args.Count > 0 ? SettingField(o.Args[0]) : null;
                if (f == null || f.FieldType != typeof(bool)) return Err("Not an on/off setting.");
                f.SetValue(Store.Cfg, !(bool)f.GetValue(Store.Cfg));
                AfterSet(f.Name);
                return Ok(f.Name + " = " + ((bool)f.GetValue(Store.Cfg) ? "on" : "off"));
            });
            Add("config export", "config export FILE [--name N]", "Write the shareable settings (no accounts) to a .fcfg file.", delegate(Opts o)
            {
                if (o.Args.Count == 0) return Err("Give the file.");
                Configs.Export(o.Args[0], o.Get("name", Path.GetFileNameWithoutExtension(o.Args[0])));
                return Ok("Wrote " + o.Args[0] + ".");
            });
            Add("config import", "config import FILE", "Apply a .fcfg file.", delegate(Opts o)
            {
                if (o.Args.Count == 0) return Err("Give the file.");
                string name; string err = Configs.Import(o.Args[0], out name);
                if (err != null) return Err(err);
                Themes.Apply(Store.Cfg.Theme); Perf.CapChanged();
                return Ok("Applied " + name + ".");
            });

            Add("theme list", "theme list", "All themes.", delegate(Opts o) { return Ok(string.Join("\n", Themes.Names().Select(n => (n == Store.Cfg.Theme ? "* " : "  ") + n).ToArray())); });
            Add("theme set", "theme set NAME [--accent N]", "Switch theme.", delegate(Opts o)
            {
                string n = o.Rest(0);
                string name = Themes.Names().FirstOrDefault(x => x.Equals(n, StringComparison.OrdinalIgnoreCase));
                if (name == null) return Err("No theme called " + n + ". Try: theme list");
                Store.Cfg.Theme = name;
                if (o.Has("accent")) Store.Cfg.Accent = Math.Max(0, Math.Min(Pal.Accents.Length - 1, o.Int("accent", 0)));
                AfterSet("Theme");
                return Ok("Theme: " + name);
            });
            Add("theme export", "theme export FILE", "Save the current theme to a .ftheme file.", delegate(Opts o) { if (o.Args.Count == 0) return Err("Give the file."); string e = Themes.Export(o.Args[0]); return e == null ? Ok("Wrote " + o.Args[0] + ".") : Err(e); });
            Add("theme import", "theme import FILE", "Add a .ftheme file.", delegate(Opts o) { if (o.Args.Count == 0) return Err("Give the file."); string e = Themes.Import(o.Args[0]); return e == null ? Ok("Imported.") : Err(e); });

            Add("perf apply", "perf apply", "Apply the frame, CPU and memory limits to the running windows now.", delegate(Opts o) { Perf.CapChanged(); Perf.Apply(); return Ok("ok"); });
            Add("perf fps", "perf fps N|off", "Frame rate limit for every window (0 or off removes it).", delegate(Opts o)
            {
                string v = o.Rest(0).ToLowerInvariant(); int n = v == "off" ? 0 : -1;
                if (n < 0 && !int.TryParse(v, out n)) return Err("Give a number or off.");
                Store.Cfg.FpsCap = Math.Max(0, n); AfterSet("FpsCap");
                return Ok("Frame limit: " + (n == 0 ? "off" : n.ToString()));
            });
            Add("perf graphics", "perf graphics Default|Lowest|Low|Light|High|Best", "Graphics preset for windows started afterwards.", delegate(Opts o)
            {
                string g = Perf.GfxNames.FirstOrDefault(x => x.Equals(o.Rest(0), StringComparison.OrdinalIgnoreCase));
                if (g == null) return Err("Choose one of: " + string.Join(", ", Perf.GfxNames));
                Store.Cfg.GfxPreset = g; AfterSet("GfxPreset");
                return Ok("Graphics: " + g);
            });
            Add("perf priority", "perf priority Normal|BelowNormal|Idle", "Process priority of the Roblox windows.", delegate(Opts o)
            {
                string p = new string[] { "Normal", "BelowNormal", "Idle" }.FirstOrDefault(x => x.Equals(o.Rest(0), StringComparison.OrdinalIgnoreCase));
                if (p == null) return Err("Choose Normal, BelowNormal or Idle.");
                Store.Cfg.Priority = p; AfterSet("Priority");
                return Ok("Priority: " + p);
            });
            Add("perf quick", "perf quick [NAME]", "List the one-click configs, or apply one.", delegate(Opts o)
            {
                if (o.Args.Count == 0) return Ok(string.Join("\n", Configs.Quick.Select(c => c.Name.PadRight(24) + c.Tagline).ToArray()));
                Config quick = Configs.Quick.FirstOrDefault(x => x.Name.Equals(o.Rest(0), StringComparison.OrdinalIgnoreCase));
                if (quick == null) return Err("No config called " + o.Rest(0) + ".");
                quick.Apply(Store.Cfg); Perf.CapChanged(); AfterSet("GfxPreset");
                return Ok("Applied " + quick.Name + ".");
            });
            Add("perf trim", "perf trim", "Trim the memory of the Roblox windows.", delegate(Opts o) { long n = Engine.TrimMemory(); return Ok("Freed about " + (n >> 20) + " MB."); });
            Add("perf memclean", "perf memclean", "Clean standby memory of the whole PC (some steps need administrator rights).", delegate(Opts o) { return Ok(MemClean.Clean()); });

            Add("optimizer status", "optimizer status", "What the optimizer would change, and the Roblox folders it found.", delegate(Opts o)
            {
                Settings c = Store.Cfg;
                StringBuilder sb = new StringBuilder();
                foreach (string d in Optimizer.VersionDirs()) sb.AppendLine(d + (Optimizer.CanWrite(d) ? "" : "   (no write access: needs administrator)"));
                sb.AppendLine("cursor " + (c.OptCursor ? "on" : "off") + ", font " + (c.OptFont ? c.OptFontName : "off") + ", black and white " + (c.OptMono ? "on" : "off") + ", clean old versions " + (c.OptCleanOld ? "on" : "off") + ", auto " + (c.OptAuto ? "on" : "off"));
                sb.AppendLine("Change them with: config set OptCursor off  (OptFont, OptFontName, OptFontFile, OptMono, OptMonoMenu, OptMonoEmotes, OptMonoHud, OptMonoOther, OptCleanOld, OptCleanLogs, OptCleanCache, OptAuto)");
                return Ok(sb.ToString().TrimEnd());
            });
            Add("optimizer apply", "optimizer apply", "Apply the optimizer to every Roblox version found.", delegate(Opts o)
            {
                Task<string> t = Task.Run(() => Optimizer.Apply());
                return new Res { Done = delegate { return t.IsCompleted; }, After = delegate { return t.IsFaulted ? "Failed: " + t.Exception.GetBaseException().Message : t.Result; }, TimeoutMs = 900000 };
            });
            Add("optimizer restore", "optimizer restore", "Put every changed Roblox file back.", delegate(Opts o)
            {
                Task<int> t = Task.Run(() => Optimizer.Restore());
                return new Res { Done = delegate { return t.IsCompleted; }, After = delegate { return t.IsFaulted ? "Failed: " + t.Exception.GetBaseException().Message : "Put back " + t.Result + " file(s)."; }, TimeoutMs = 900000 };
            });

            Add("multi", "multi [on|off]", "Multi-instance (several Roblox windows). Without an argument: show it.", delegate(Opts o)
            {
                if (o.Args.Count > 0) { Store.Cfg.MultiInstance = o.Args[0].ToLowerInvariant() == "on"; AfterSet("MultiInstance"); }
                return Ok("Multi-instance: " + (Engine.MultiActive ? "on" : "off"));
            });
        }
    }
}
