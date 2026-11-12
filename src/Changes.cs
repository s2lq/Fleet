using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Fleet
{
    public class ChangeEntry { public string Version = ""; public List<string> Items = new List<string>(); }

    // the changelog that is built into the exe
    public static class Changes
    {
        static List<ChangeEntry> cache;

        public static List<ChangeEntry> All()
        {
            if (cache != null) return cache;
            string text = "";
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Fleet.CHANGELOG.md"))
                    if (s != null) using (StreamReader r = new StreamReader(s)) text = r.ReadToEnd();
            }
            catch (Exception) { }
            cache = Parse(text);
            return cache;
        }

        public static List<ChangeEntry> Parse(string text)
        {
            List<ChangeEntry> list = new List<ChangeEntry>();
            ChangeEntry cur = null;
            foreach (string raw in (text ?? "").Replace("\r", "").Split('\n'))
            {
                if (raw.StartsWith("## ")) { cur = new ChangeEntry { Version = raw.Substring(3).Trim() }; list.Add(cur); }
                else if (cur != null && raw.StartsWith("- ")) cur.Items.Add(raw.Substring(2).Trim());
            }
            return list;
        }

        public static bool Newer(string a, string b)
        {
            Version va, vb;
            return Version.TryParse(a, out va) && Version.TryParse(b, out vb) && va > vb;
        }

        // everything newer than the version you had before (at most five), newest first
        public static List<ChangeEntry> Since(string last)
        {
            List<ChangeEntry> all = All();
            if (string.IsNullOrEmpty(last)) return all.Take(1).ToList();
            return all.Where(e => Newer(e.Version, last)).Take(5).ToList();
        }
    }
}
