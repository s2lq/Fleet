using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;

namespace Fleet
{
    public class Config
    {
        public string Name, Tagline, Text;
        public Action<Settings> Apply;
        public Config(string n, string t, string x, Action<Settings> a) { Name = n; Tagline = t; Text = x; Apply = a; }
    }

    public static class Configs
    {
        public static readonly List<Config> Quick = new List<Config>
        {
            new Config("Ultimate performance", "most alts on the weakest pc", "Lowest graphics, 15 FPS, 250 MB and 6 % CPU per window, background windows slowed.",
                delegate(Settings s) { s.GfxPreset = "Lowest"; s.FpsCap = 15; s.Priority = "BelowNormal"; s.ThrottleBackground = true; s.RamCapMode = "Auto250"; s.CpuCapMode = "Each"; s.CpuCapPercent = 6; }),
            new Config("Performance", "many alts, still playable", "Low graphics, 20 FPS, 250 MB and 10 % CPU per window.",
                delegate(Settings s) { s.GfxPreset = "Low"; s.FpsCap = 20; s.Priority = "BelowNormal"; s.ThrottleBackground = true; s.RamCapMode = "Auto250"; s.CpuCapMode = "Each"; s.CpuCapPercent = 10; }),
            new Config("Balanced", "a good middle", "Light graphics, 30 FPS, up to 500 MB per window, no CPU cap.",
                delegate(Settings s) { s.GfxPreset = "Light"; s.FpsCap = 30; s.Priority = "Normal"; s.ThrottleBackground = true; s.RamCapMode = "Fixed500"; s.CpuCapMode = "Off"; }),
            new Config("Quality", "nice looking, still sensible", "Roblox's own graphics, 60 FPS, no limits.",
                delegate(Settings s) { s.GfxPreset = "Default"; s.FpsCap = 60; s.Priority = "Normal"; s.ThrottleBackground = false; s.RamCapMode = "Off"; s.CpuCapMode = "Off"; }),
            new Config("Best visuals", "everything on high", "Highest graphics, 120 FPS, no limits. Needs a strong PC.",
                delegate(Settings s) { s.GfxPreset = "Best"; s.FpsCap = 120; s.Priority = "Normal"; s.ThrottleBackground = false; s.RamCapMode = "Off"; s.CpuCapMode = "Off"; })
        };

        public static readonly List<Config> Community = new List<Config>
        {
            new Config("AFK farm overnight", "leave it running all night", "Lowest graphics, 10 FPS, anti-AFK, auto-relaunch with rejoin, logins checked every 3 hours.",
                delegate(Settings s) { s.GfxPreset = "Lowest"; s.FpsCap = 10; s.Priority = "Idle"; s.ThrottleBackground = true; s.RamCapMode = "Auto250"; s.CpuCapMode = "Each"; s.CpuCapPercent = 5; s.AntiAfk = true; s.AntiAfkMin = 10; s.AutoRelaunch = true; s.RejoinSame = true; s.HealthAuto = true; s.HealthHours = 3; }),
            new Config("Low-end laptop", "weak cpu, little ram", "Lowest graphics, 20 FPS, 250 MB each, total CPU capped at 60 %.",
                delegate(Settings s) { s.GfxPreset = "Lowest"; s.FpsCap = 20; s.Priority = "BelowNormal"; s.ThrottleBackground = true; s.RamCapMode = "Auto250"; s.CpuCapMode = "Total"; s.CpuCapPercent = 60; s.HealthAuto = false; }),
            new Config("Alt army (20+)", "as many windows as the pc can hold", "Lowest graphics, 12 FPS, 250 MB and 4 % CPU each. Needs 32 GB of RAM or more.",
                delegate(Settings s) { s.GfxPreset = "Lowest"; s.FpsCap = 12; s.Priority = "BelowNormal"; s.ThrottleBackground = true; s.RamCapMode = "Auto250"; s.CpuCapMode = "Each"; s.CpuCapPercent = 4; }),
            new Config("Stream and play", "smooth main window, quiet alts", "Light graphics, 60 FPS, the windows you are not looking at are slowed.",
                delegate(Settings s) { s.GfxPreset = "Light"; s.FpsCap = 60; s.Priority = "Normal"; s.ThrottleBackground = true; s.RamCapMode = "Fixed500"; s.CpuCapMode = "Each"; s.CpuCapPercent = 12; }),
            new Config("Battery saver", "longest time on battery", "Lowest graphics, 10 FPS, low priority, 3 % CPU each, slower checks.",
                delegate(Settings s) { s.GfxPreset = "Lowest"; s.FpsCap = 10; s.Priority = "Idle"; s.ThrottleBackground = true; s.RamCapMode = "Auto250"; s.CpuCapMode = "Each"; s.CpuCapPercent = 3; s.WatcherSec = 120; s.HealthAuto = false; })
        };

        // settings that are safe to share: never accounts, cookies, proxies, webhooks or servers
        static readonly string[] Shared =
        {
            "GfxPreset", "FpsCap", "Priority", "ThrottleBackground", "BackgroundFps", "CpuCapMode", "CpuCapPercent", "RamCapMode", "AutoTrim", "TrimEveryMin",
            "LaunchMethod", "StaggerMs", "UseOwn", "MultiInstance", "AntiAfk", "AntiAfkMin", "AutoRelaunch", "WatcherSec", "RejoinSame", "Breaker", "BreakerMax", "BreakerCooldownMin",
            "HealthAuto", "HealthHours", "Theme", "Accent", "Wallpaper", "ReduceMotion", "LayoutGap", "LayoutCols", "LayoutBorderless", "LayoutTopMost", "LayoutRename", "CustomThemes"
        };

        static readonly JavaScriptSerializer js = new JavaScriptSerializer();

        public static void Export(string file, string name)
        {
            Dictionary<string, object> map = new Dictionary<string, object>();
            foreach (string k in Shared) { FieldInfo f = typeof(Settings).GetField(k); if (f != null) map[k] = f.GetValue(Store.Cfg); }
            File.WriteAllText(file, js.Serialize(new Dictionary<string, object> { { "format", "fleet-config" }, { "version", 1 }, { "name", name }, { "settings", map } }));
        }

        public static string Import(string file, out string name)
        {
            name = "";
            try
            {
                Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(File.ReadAllText(file));
                if (!d.ContainsKey("format") || Convert.ToString(d["format"]) != "fleet-config") return "That is not a Fleet config.";
                name = d.ContainsKey("name") ? Convert.ToString(d["name"]) : "";
                Dictionary<string, object> s = (Dictionary<string, object>)d["settings"];
                foreach (string k in Shared)
                {
                    FieldInfo f = typeof(Settings).GetField(k);
                    if (f == null || !s.ContainsKey(k)) continue;
                    try { f.SetValue(Store.Cfg, js.ConvertToType(s[k], f.FieldType)); } catch (Exception) { }
                }
                Store.SaveSettings();
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }
    }
}
