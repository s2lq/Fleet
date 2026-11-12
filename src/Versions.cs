using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Fleet
{
    // download an older (or the newest) Roblox player from Roblox's own servers into Fleet's folder
    public static class Versions
    {
        const string Cdn = "https://setup.rbxcdn.com/";
        public static string Root { get { return Path.Combine(Store.Dir, "RobloxVersions"); } }

        public static float Progress;
        public static string Current = "";
        public static volatile bool CancelRequested;

        public static string ExeFor(Account a)
        {
            string v = a != null && !string.IsNullOrEmpty(a.RobloxVersion) ? a.RobloxVersion : Store.Cfg.RobloxVersion;
            if (string.IsNullOrEmpty(v)) return null;
            string exe = Path.Combine(Root, v, "RobloxPlayerBeta.exe");
            return File.Exists(exe) ? exe : null;
        }

        public static long SizeOf(string v)
        {
            try { return new DirectoryInfo(Path.Combine(Root, v)).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); } catch (Exception) { return 0; }
        }

        public static string Delete(string v)
        {
            try { Directory.Delete(Path.Combine(Root, v), true); if (Store.Cfg.RobloxVersion == v) Store.Cfg.RobloxVersion = ""; foreach (Account a in Store.Accounts) if (a.RobloxVersion == v) a.RobloxVersion = ""; Store.SaveSettings(); Store.SaveAccounts(); return null; }
            catch (Exception ex) { return ex.Message; }
        }

        public static string PinnedExe()
        {
            string v = Store.Cfg.RobloxVersion;
            if (string.IsNullOrEmpty(v)) return null;
            string exe = Path.Combine(Root, v, "RobloxPlayerBeta.exe");
            return File.Exists(exe) ? exe : null;
        }

        public static List<string> Installed()
        {
            if (!Directory.Exists(Root)) return new List<string>();
            return Directory.GetDirectories(Root).Select(d => Path.GetFileName(d)).Where(n => File.Exists(Path.Combine(Root, n, "RobloxPlayerBeta.exe"))).ToList();
        }

        public class Release { public string Version = ""; public string Date = ""; }

        // newest first
        public static async Task<List<Release>> Recent()
        {
            string text = await Roblox.GetText(Cdn + "DeployHistory.txt");
            List<Release> list = new List<Release>();
            foreach (Match m in Regex.Matches(text, @"New WindowsPlayer (version-[0-9a-f]+) at (\S+ \S+ \S+)"))
                list.Add(new Release { Version = m.Groups[1].Value, Date = m.Groups[2].Value });
            list.Reverse();
            // the history file can lag behind, so ask Roblox for the current version too
            try
            {
                string json = await Roblox.GetText("https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer");
                Match lm = Regex.Match(json, "\"clientVersionUpload\"\\s*:\\s*\"(version-[0-9a-f]+)\"");
                if (lm.Success && !list.Any(r => r.Version == lm.Groups[1].Value)) list.Insert(0, new Release { Version = lm.Groups[1].Value, Date = "current" });
            }
            catch (Exception) { }
            return list.Take(20).ToList();
        }

        public static async Task<string> Download(string version, Action<string> progress)
        {
            string dir = Path.Combine(Root, version);
            if (File.Exists(Path.Combine(dir, "RobloxPlayerBeta.exe"))) return null;
            CancelRequested = false; Current = version; Progress = 0;
            try
            {
                Directory.CreateDirectory(dir);
                string manifest = await Roblox.GetText(Cdn + version + "-rbxPkgManifest.txt");
                string[] lines = manifest.Replace("\r", "").Split('\n');
                List<string[]> pkgs = new List<string[]>();
                for (int i = 1; i + 3 < lines.Length; i += 4) pkgs.Add(new string[] { lines[i], lines[i + 1] });
                int n = 0;
                foreach (string[] p in pkgs)
                {
                    n++;
                    if (CancelRequested) throw new OperationCanceledException("Download cancelled.");
                    Progress = (n - 1) / (float)pkgs.Count;
                    if (progress != null) progress("Downloading " + n + "/" + pkgs.Count + ": " + p[0]);
                    byte[] data = await Roblox.GetBytes(Cdn + version + "-" + p[0], null);
                    using (MD5 md5 = MD5.Create())
                        if (BitConverter.ToString(md5.ComputeHash(data)).Replace("-", "").ToLower() != p[1].ToLower()) throw new InvalidDataException(p[0] + " is damaged.");
                    string target = Where(p[0]);
                    string tmp = Path.Combine(dir, p[0]);
                    File.WriteAllBytes(tmp, data);
                    ZipFile.ExtractToDirectory(tmp, Path.Combine(dir, target));
                    File.Delete(tmp);
                }
                Progress = 1f;
                File.WriteAllText(Path.Combine(dir, "AppSettings.xml"), "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Settings>\r\n\t<ContentFolder>content</ContentFolder>\r\n\t<BaseUrl>http://www.roblox.com</BaseUrl>\r\n</Settings>\r\n");
                return File.Exists(Path.Combine(dir, "RobloxPlayerBeta.exe")) ? null : "The download finished but the player was not in it.";
            }
            catch (Exception ex) { try { Directory.Delete(dir, true); } catch (Exception) { } Current = ""; return ex.Message; }
            finally { if (Progress >= 1f) Current = ""; }
        }

        // where each package goes inside the version folder
        static string Where(string pkg)
        {
            switch (pkg)
            {
                case "content-avatar.zip": return "content\\avatar";
                case "content-configs.zip": return "content\\configs";
                case "content-fonts.zip": return "content\\fonts";
                case "content-sky.zip": return "content\\sky";
                case "content-sounds.zip": return "content\\sounds";
                case "content-textures2.zip": return "content\\textures";
                case "content-models.zip": return "content\\models";
                case "content-platform-fonts.zip": return "PlatformContent\\pc\\fonts";
                case "content-platform-dictionaries.zip": return "PlatformContent\\pc\\shaders";
                case "content-terrain.zip": return "PlatformContent\\pc\\terrain";
                case "content-textures3.zip": return "PlatformContent\\pc\\textures";
                case "extracontent-luapackages.zip": return "ExtraContent\\LuaPackages";
                case "extracontent-translations.zip": return "ExtraContent\\translations";
                case "extracontent-models.zip": return "ExtraContent\\models";
                case "extracontent-textures.zip": return "ExtraContent\\textures";
                case "shaders.zip": return "shaders";
                case "ssl.zip": return "ssl";
                default: return "";
            }
        }
    }
}
