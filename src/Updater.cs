using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Fleet
{
    // looks for a newer release on GitHub, checks its SHA-256 and swaps the exe
    public static class Updater
    {
        public const string Repo = "s2lq/Fleet";
        public static string Latest = "";
        public static string ExeUrl = "", SumUrl = "";

        public static bool Newer(string a, string b)
        {
            Version va, vb;
            return Version.TryParse(a.TrimStart('v'), out va) && Version.TryParse(b.TrimStart('v'), out vb) && va > vb;
        }

        public static async Task<bool> Check()
        {
            try
            {
                string json = await Roblox.GetText("https://api.github.com/repos/" + Repo + "/releases/latest");
                Dictionary<string, object> d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                Latest = Convert.ToString(d["tag_name"]);
                foreach (object o in (System.Collections.ArrayList)d["assets"])
                {
                    Dictionary<string, object> a = (Dictionary<string, object>)o;
                    string n = Convert.ToString(a["name"]), u = Convert.ToString(a["browser_download_url"]);
                    if (n.EndsWith(".exe")) ExeUrl = u; else if (n.StartsWith("SHA256")) SumUrl = u;
                }
                return Newer(Latest, Program.Version);
            }
            catch (Exception) { return false; }
        }

        public static async Task<string> Install()
        {
            try
            {
                if (ExeUrl.Length == 0) return "The release has no exe.";
                byte[] exe = await Roblox.GetBytes(ExeUrl, null);
                if (SumUrl.Length > 0)
                {
                    string sums = await Roblox.GetText(SumUrl);
                    Match m = Regex.Match(sums, @"([0-9a-fA-F]{64})");
                    string got;
                    using (SHA256 sha = SHA256.Create()) got = BitConverter.ToString(sha.ComputeHash(exe)).Replace("-", "").ToLower();
                    if (!m.Success || m.Groups[1].Value.ToLower() != got) return "The download does not match its checksum, so it was not installed.";
                }
                string self = Application.ExecutablePath, tmp = self + ".new", old = self + ".old";
                File.WriteAllBytes(tmp, exe);
                if (File.Exists(old)) File.Delete(old);
                File.Move(self, old);
                File.Move(tmp, self);
                Process.Start(new ProcessStartInfo(self, "--wait-pid " + Process.GetCurrentProcess().Id) { UseShellExecute = false });
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }
    }
}
