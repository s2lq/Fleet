using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace Fleet
{
    // all dark: they differ in how black the background is and what tint the panels have
    public static class Themes
    {
        public class Def { public string Name; public Color Bg, Panel, Line, Text, Muted; }

        static Def D(string n, string bg, string panel, string line, string text, string muted)
        {
            return new Def { Name = n, Bg = Hex(bg), Panel = Hex(panel), Line = Hex(line), Text = Hex(text), Muted = Hex(muted) };
        }

        public static readonly List<Def> Presets = new List<Def>
        {
            D("Black",    "#000000", "#0B0B0B", "#222222", "#F0F0F0", "#969696"),
            D("Carbon",   "#080808", "#111111", "#2A2A2A", "#EDEDED", "#8E8E8E"),
            D("Graphite", "#0E0E10", "#18181B", "#2E2E33", "#ECECEF", "#94949C"),
            D("Slate",    "#101114", "#1A1C21", "#30333B", "#E8E9EE", "#9094A0"),
            D("Midnight", "#04060C", "#0C0F18", "#1C2236", "#E4E9F7", "#7F89A8"),
            D("Forest",   "#050907", "#0C130F", "#1E2D24", "#E4F0E8", "#82988A"),
            D("Wine",     "#0A0506", "#140C0E", "#2E1C20", "#F2E6E8", "#A48A90"),
            D("Ink",      "#0A0908", "#141211", "#2B2825", "#EFEBE6", "#9A948C")
        };

        public static Color Hex(string h)
        {
            try
            {
                h = (h ?? "").Trim().TrimStart('#');
                if (h.Length == 6) return Color.FromArgb(Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16), Convert.ToInt32(h.Substring(4, 2), 16));
            }
            catch (Exception) { }
            return Color.Empty;
        }

        public static string ToHex(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }

        public static List<string> Names() { return Presets.Select(p => p.Name).Concat(Store.Cfg.CustomThemes.Select(c => c.Name)).ToList(); }

        static Color Lift(Color c, int d) { return Color.FromArgb(Math.Min(255, c.R + d), Math.Min(255, c.G + d), Math.Min(255, c.B + d)); }

        public static void Apply(string name)
        {
            Def d = Presets.FirstOrDefault(p => p.Name == name);
            if (d == null)
            {
                CustomTheme c = Store.Cfg.CustomThemes.FirstOrDefault(x => x.Name == name);
                if (c != null) d = new Def { Name = c.Name, Bg = Hex(c.Bg), Panel = Hex(c.Panel), Line = Hex(c.Line), Text = Hex(c.Text), Muted = Hex(c.Muted) };
            }
            if (d == null || d.Bg.IsEmpty || d.Panel.IsEmpty || d.Line.IsEmpty || d.Text.IsEmpty || d.Muted.IsEmpty) d = Presets[0];
            Pal.Bg = d.Bg; Pal.Panel = d.Panel; Pal.Line = d.Line; Pal.Text = d.Text; Pal.Muted = d.Muted;
            Pal.Panel2 = Lift(d.Panel, 10); Pal.Line2 = Lift(d.Line, 40);
            Pal.Faint = Color.FromArgb((d.Muted.R + d.Bg.R) / 2 - 20 > 0 ? (d.Muted.R + d.Bg.R) / 2 - 20 : 40, (d.Muted.G + d.Bg.G) / 2 - 20 > 0 ? (d.Muted.G + d.Bg.G) / 2 - 20 : 40, (d.Muted.B + d.Bg.B) / 2 - 20 > 0 ? (d.Muted.B + d.Bg.B) / 2 - 20 : 40);
        }

        public static CustomTheme Current()
        {
            Def d = Presets.FirstOrDefault(p => p.Name == Store.Cfg.Theme) ?? Presets[0];
            return new CustomTheme { Name = d.Name + " copy", Bg = ToHex(d.Bg), Panel = ToHex(d.Panel), Line = ToHex(d.Line), Text = ToHex(d.Text), Muted = ToHex(d.Muted) };
        }

        public static string Export(string file)
        {
            try
            {
                CustomTheme c = Store.Cfg.CustomThemes.FirstOrDefault(x => x.Name == Store.Cfg.Theme) ?? Current();
                File.WriteAllText(file, new JavaScriptSerializer().Serialize(c));
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public static string Import(string file)
        {
            try
            {
                CustomTheme c = new JavaScriptSerializer().Deserialize<CustomTheme>(File.ReadAllText(file));
                if (string.IsNullOrEmpty(c.Name) || Hex(c.Bg).IsEmpty || Hex(c.Panel).IsEmpty || Hex(c.Line).IsEmpty || Hex(c.Text).IsEmpty || Hex(c.Muted).IsEmpty) return "That theme file is not valid.";
                if (Presets.Any(p => p.Name == c.Name)) c.Name += " (mine)";
                Store.Cfg.CustomThemes.RemoveAll(x => x.Name == c.Name);
                Store.Cfg.CustomThemes.Add(c);
                Store.Cfg.Theme = c.Name; Store.SaveSettings(); Apply(c.Name);
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }
    }
}
