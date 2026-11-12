using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Fleet
{
    public class Imp { public string Cookie = "", User = "", Pass = "", Alias = "", Group = "", Note = ""; }

    // reads account lists exported by other managers (JSON with a security token or cookie field, or text lines)
    public static class Importer
    {
        public static List<Imp> FromFile(string path)
        {
            string text = File.ReadAllText(path);
            List<Imp> list = new List<Imp>();
            try
            {
                object root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(text);
                Walk(root, list);
            }
            catch (Exception) { }
            if (list.Count == 0)
            {
                foreach (string line in text.Replace("\r", "").Split('\n'))
                {
                    string c = Roblox.ExtractCookie(line);
                    if (c == null) continue;
                    Imp i = new Imp { Cookie = c };
                    Match m = Regex.Match(line, @"^\s*([^:\s]+):([^:\s]*):_\|WARNING");
                    if (m.Success) { i.User = m.Groups[1].Value; i.Pass = m.Groups[2].Value; }
                    list.Add(i);
                }
            }
            List<Imp> unique = new List<Imp>();
            HashSet<string> seen = new HashSet<string>();
            foreach (Imp i in list) if (seen.Add(i.Cookie)) unique.Add(i);
            return unique;
        }

        static string Str(IDictionary d, params string[] keys)
        {
            foreach (DictionaryEntry e in d)
                foreach (string k in keys)
                    if (string.Equals(Convert.ToString(e.Key), k, StringComparison.OrdinalIgnoreCase) && e.Value is string) return (string)e.Value;
            return "";
        }

        static void Walk(object o, List<Imp> list)
        {
            IDictionary d = o as IDictionary;
            if (d != null)
            {
                string cookie = Str(d, "SecurityToken", "Cookie", ".ROBLOSECURITY", "ROBLOSECURITY", "Token");
                if (cookie.StartsWith("_|WARNING"))
                {
                    list.Add(new Imp { Cookie = cookie, User = Str(d, "Username", "User", "Name"), Pass = Str(d, "Password", "Pass"), Alias = Str(d, "Alias", "Nickname"), Group = Str(d, "Group"), Note = Str(d, "Description", "Note", "Notes") });
                    return;
                }
                foreach (DictionaryEntry e in d) Walk(e.Value, list);
                return;
            }
            IEnumerable arr = o as IEnumerable;
            if (arr != null && !(o is string))
                foreach (object x in arr)
                {
                    string s = x as string;
                    if (s != null && s.StartsWith("_|WARNING")) list.Add(new Imp { Cookie = s });
                    else Walk(x, list);
                }
        }
    }
}
