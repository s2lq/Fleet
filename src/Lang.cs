using System;
using System.Collections.Generic;
using System.IO;

namespace Fleet
{
    // text is looked up by its English wording; whatever has no translation stays English
    public static class Lang
    {
        static readonly Dictionary<string, string> map = new Dictionary<string, string>();
        public static string Current = "en";

        public static List<string[]> Available()
        {
            List<string[]> l = new List<string[]>();
            l.Add(new string[] { "en", "English" });
            for (int i = 0; i < LangPacks.Codes.Length; i++) l.Add(new string[] { LangPacks.Codes[i], LangPacks.Names[i] });
            try
            {
                string dir = Path.Combine(Store.Dir, "lang");
                if (Directory.Exists(dir))
                    foreach (string f in Directory.GetFiles(dir, "*.json"))
                    {
                        string code = Path.GetFileNameWithoutExtension(f);
                        if (code != "en" && Array.IndexOf(LangPacks.Codes, code) < 0) l.Add(new string[] { code, code });
                    }
            }
            catch (Exception) { }
            return l;
        }

        public static void Load(string code)
        {
            Current = string.IsNullOrEmpty(code) ? "en" : code;
            map.Clear();
            if (Current == "en") return;
            foreach (KeyValuePair<string, string> kv in LangPacks.Get(Current)) map[kv.Key] = kv.Value;
            try
            {
                string f = Path.Combine(Store.Dir, "lang", Current + ".json");
                if (File.Exists(f))
                {
                    System.Collections.IDictionary d = new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(File.ReadAllText(f)) as System.Collections.IDictionary;
                    if (d != null) foreach (System.Collections.DictionaryEntry e in d) map[Convert.ToString(e.Key)] = Convert.ToString(e.Value);
                }
            }
            catch (Exception) { }
        }

        public static string T(string s)
        {
            if (Current == "en" || string.IsNullOrEmpty(s)) return s;
            string v;
            return map.TryGetValue(s, out v) && !string.IsNullOrEmpty(v) ? v : s;
        }

        public static string F(string format, params object[] args) { return string.Format(T(format), args); }
    }
}
