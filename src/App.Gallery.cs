using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Fleet
{
    // configs and themes shared by other people, listed in gallery/index.json of the Fleet repository
    public partial class App
    {
        class GalleryItem { public string Type, Name, By, About, File; }
        const string GalleryBase = "https://raw.githubusercontent.com/" + Updater.Repo + "/main/gallery/";
        List<GalleryItem> gallery = new List<GalleryItem>();
        string galStatus = "";
        bool galLoaded;

        // for the command line: the gallery as rows of type, name, author, about, file
        public async Task<List<string[]>> GalleryRows()
        {
            string json = await Roblox.GetText(GalleryBase + "index.json");
            Dictionary<string, object> d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            List<string[]> rows = new List<string[]>();
            foreach (object o in (System.Collections.ArrayList)d["items"])
            {
                Dictionary<string, object> it = (Dictionary<string, object>)o;
                rows.Add(new string[] { Convert.ToString(it["type"]), Convert.ToString(it["name"]), it.ContainsKey("by") ? Convert.ToString(it["by"]) : "", it.ContainsKey("about") ? Convert.ToString(it["about"]) : "", Convert.ToString(it["file"]) });
            }
            return rows;
        }

        public async Task<string> GalleryInstallByName(string name)
        {
            foreach (string[] r in await GalleryRows())
                if (r[1].Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    InstallGallery(new GalleryItem { Type = r[0], Name = r[1], By = r[2], About = r[3], File = r[4] });
                    return "Installing " + r[1] + ".";
                }
            return null;
        }

        async void LoadGallery()
        {
            galStatus = "Loading...";
            try
            {
                string json = await Roblox.GetText(GalleryBase + "index.json");
                Dictionary<string, object> d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                gallery.Clear();
                foreach (object o in (System.Collections.ArrayList)d["items"])
                {
                    Dictionary<string, object> it = (Dictionary<string, object>)o;
                    gallery.Add(new GalleryItem { Type = Convert.ToString(it["type"]), Name = Convert.ToString(it["name"]), By = it.ContainsKey("by") ? Convert.ToString(it["by"]) : "", About = it.ContainsKey("about") ? Convert.ToString(it["about"]) : "", File = Convert.ToString(it["file"]) });
                }
                galStatus = gallery.Count + " items";
            }
            catch (Exception ex) { galStatus = "Could not load the gallery: " + ex.Message; }
            galLoaded = true;
        }

        async void InstallGallery(GalleryItem g)
        {
            try
            {
                string text = await Roblox.GetText(GalleryBase + g.File);
                string tmp = Path.Combine(Path.GetTempPath(), "fleet-gallery-" + Guid.NewGuid().ToString("N").Substring(0, 6) + (g.Type == "theme" ? ".ftheme" : ".fcfg"));
                File.WriteAllText(tmp, text);
                string name;
                string err = g.Type == "theme" ? Themes.Import(tmp) : Configs.Import(tmp, out name);
                try { File.Delete(tmp); } catch (Exception) { }
                if (err != null) { Log.Add("Could not install " + g.Name + ": " + err); return; }
                if (g.Type != "theme") { Themes.Apply(Store.Cfg.Theme); Perf.CapChanged(); }
                Log.Add("Installed " + g.Name + ".");
            }
            catch (Exception ex) { Log.Add("Could not install " + g.Name + ": " + ex.Message); }
        }

        void DrawGallery(Ui ui, RectangleF a)
        {
            if (!galLoaded && galStatus.Length == 0) LoadGallery();
            float by = a.Y + 84;
            if (ui.Button("ga.ref", new RectangleF(a.X, by, 150, 42), "Refresh", Btn.Ghost, "refresh")) LoadGallery();
            if (galStatus == "Loading...") ui.Spinner(new RectangleF(a.X + 166, by + 12, 18, 18), Pal.Muted);
            ui.Text(Ui.Fit(ui, galStatus, Fonts.Body, a.Width - 400), a.X + (galStatus == "Loading..." ? 196 : 166), by + 12, Fonts.Body, Pal.Muted);
            ui.Text("Share your own: send a pull request to the gallery folder.", a.Right - 380, by + 12, Fonts.Small, Pal.Faint);
            RectangleF view = new RectangleF(a.X - 8, by + 58, a.Width + 16, a.Bottom - by - 58);
            if (gallery.Count == 0) { ui.TextIn(galLoaded ? "The gallery is empty." : "", view, Fonts.Body, Pal.Muted, StringAlignment.Center); return; }
            float rh = 78;
            ui.BeginScroll(view, gallery.Count * (rh + 8) + 10, Scroll("gallery"));
            float y = view.Y + 4;
            foreach (GalleryItem g in gallery)
            {
                RectangleF r = new RectangleF(a.X, y, a.Width, rh);
                ui.Glass(r, 14, 0);
                ui.Text(g.Name, r.X + 24, r.Y + 14, Fonts.BodyB, Pal.Text);
                ui.Chip(g.Type == "theme" ? "Theme" : "Config", r.X + 24 + ui.Measure(g.Name, Fonts.BodyB).Width + 12, r.Y + 13, Pal.Muted);
                ui.Text(Ui.Fit(ui, g.About + (g.By.Length > 0 ? "   by " + g.By : ""), Fonts.Small, r.Width - 200), r.X + 24, r.Y + 44, Fonts.Small, Pal.Muted);
                if (ui.Button("ga." + g.File, new RectangleF(r.Right - 24 - 110, r.Y + 20, 110, 38), "Install", Btn.Primary, null)) InstallGallery(g);
                y += rh + 8;
            }
            ui.EndScroll();
        }
    }
}
