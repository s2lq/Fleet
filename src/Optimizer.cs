using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;

namespace Fleet
{
    // Changes files inside Roblox's version folders: a Windows cursor, another font, a black and white interface, and less leftover junk.
    // Nothing is injected. Every file is copied away first so it can be put back.
    public static class Optimizer
    {
        public static string Status = "";
        public static float Progress;
        public static volatile bool Running;
        static readonly object gate = new object();
        static readonly HashSet<string> done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static bool IsAdmin()
        {
            try { using (WindowsIdentity id = WindowsIdentity.GetCurrent()) return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator); }
            catch (Exception) { return false; }
        }

        public static bool CanWrite(string dir)
        {
            try
            {
                string f = Path.Combine(dir, ".fleet-write-test");
                File.WriteAllText(f, "x");
                File.Delete(f);
                return true;
            }
            catch (Exception) { return false; }
        }

        public static List<string> VersionDirs()
        {
            List<string> dirs = Engine.RobloxExes().Select(e => Path.GetDirectoryName(e)).ToList();
            try
            {
                if (Directory.Exists(Versions.Root))
                    foreach (string d in Directory.GetDirectories(Versions.Root))
                        if (File.Exists(Path.Combine(d, "RobloxPlayerBeta.exe")) && !dirs.Contains(d, StringComparer.OrdinalIgnoreCase)) dirs.Add(d);
            }
            catch (Exception) { }
            return dirs;
        }

        public static bool NeedsAdmin()
        {
            foreach (string d in VersionDirs()) if (!CanWrite(d)) return true;
            return false;
        }

        // ---------------------------------------------------------------- backups
        static string BackupDir(string verDir)
        {
            string name = Path.GetFileName(verDir);
            int h = verDir.ToLowerInvariant().GetHashCode() & 0xFFFFFF;
            return Path.Combine(Store.Dir, "RobloxBackup", name + "-" + h.ToString("x6"));
        }

        static string Rel(string verDir, string full) { return full.Substring(verDir.Length).TrimStart('\\', '/'); }

        // keep the first original of a file. A file that did not exist is noted so it can be removed again.
        static void Keep(string verDir, string full)
        {
            string bk = BackupDir(verDir);
            string rel = Rel(verDir, full);
            string dst = Path.Combine(bk, "files", rel);
            string created = Path.Combine(bk, "created.txt");
            if (File.Exists(dst)) return;
            if (File.Exists(created) && File.ReadAllLines(created).Contains(rel)) return;
            if (File.Exists(full))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(full, dst, false);
            }
            else
            {
                Directory.CreateDirectory(bk);
                if (!File.Exists(created) || !File.ReadAllLines(created).Contains(rel)) File.AppendAllText(created, rel + "\r\n");
            }
        }

        public static int Restore()
        {
            int n = 0;
            foreach (string v in VersionDirs())
            {
                string bk = BackupDir(v);
                if (!Directory.Exists(bk)) continue;
                try
                {
                    string files = Path.Combine(bk, "files");
                    if (Directory.Exists(files))
                        foreach (string f in Directory.GetFiles(files, "*", SearchOption.AllDirectories))
                        {
                            string target = Path.Combine(v, Rel(files, f));
                            Directory.CreateDirectory(Path.GetDirectoryName(target));
                            File.Copy(f, target, true);
                            n++;
                        }
                    string created = Path.Combine(bk, "created.txt");
                    if (File.Exists(created))
                        foreach (string rel in File.ReadAllLines(created)) { try { if (rel.Length > 0) File.Delete(Path.Combine(v, rel)); } catch (Exception) { } }
                    Directory.Delete(bk, true);
                    try { File.Delete(Path.Combine(v, "FleetOpt.txt")); } catch (Exception) { }
                }
                catch (Exception ex) { Log.Add("Could not restore " + Path.GetFileName(v) + ": " + ex.Message); }
            }
            lock (gate) done.Clear();
            return n;
        }

        // ---------------------------------------------------------------- apply
        static string Signature()
        {
            Settings c = Store.Cfg;
            return string.Join("|", new string[] { "1", c.OptCursor.ToString(), c.OptFont.ToString(), c.OptFontName, c.OptFontFile, c.OptMono.ToString(), c.OptMonoMenu.ToString(), c.OptMonoEmotes.ToString(), c.OptMonoHud.ToString(), c.OptMonoOther.ToString() });
        }

        // before a launch: if the version about to start has not been done with the current choices, do it now
        public static void EnsureFor(string exe)
        {
            if (string.IsNullOrEmpty(exe)) return;
            Settings c = Store.Cfg;
            if (!c.OptCursor && !c.OptFont && !c.OptMono) return;
            string dir = Path.GetDirectoryName(exe);
            lock (gate)
            {
                string sig = Signature();
                string mark = Path.Combine(dir, "FleetOpt.txt");
                try { if (File.Exists(mark) && File.ReadAllText(mark) == sig) return; } catch (Exception) { }
                if (done.Contains(dir + sig)) return;
                done.Add(dir + sig);
                if (!CanWrite(dir)) { Log.Add("Optimizer: Fleet needs administrator rights to change " + dir + "."); return; }
                try { ApplyTo(dir, false); Log.Add("Optimizer applied to " + Path.GetFileName(dir) + "."); }
                catch (Exception ex) { Log.Add("Optimizer failed: " + ex.Message); }
            }
        }

        public static string Apply()
        {
            if (Running) return "Already working.";
            Running = true; Progress = 0; Status = "Starting";
            StringBuilder report = new StringBuilder();
            try
            {
                Settings c = Store.Cfg;
                List<string> dirs = VersionDirs();
                if (dirs.Count == 0) return "Could not find Roblox. Pick its Versions folder first.";
                long freed = 0;
                if (c.OptCleanOld || c.OptCleanLogs || c.OptCleanCache)
                {
                    Status = "Cleaning";
                    freed = Clean(report);
                    dirs = VersionDirs();
                }
                int i = 0;
                if (c.OptCursor || c.OptFont || c.OptMono)
                    foreach (string d in dirs)
                    {
                        Status = "Changing " + Path.GetFileName(d);
                        Progress = i / (float)dirs.Count;
                        if (!CanWrite(d)) { report.Append("No access to " + d + " (run Fleet as administrator). "); i++; continue; }
                        lock (gate) { ApplyTo(d, true); done.Add(d + Signature()); }
                        i++;
                    }
                Progress = 1f;
                if (freed > 0) report.Insert(0, "Freed " + (freed >> 20) + " MB. ");
                return report.Length == 0 ? "Done." : report.ToString().Trim();
            }
            catch (Exception ex) { return "Failed: " + ex.Message; }
            finally { Running = false; Status = ""; }
        }

        static void ApplyTo(string dir, bool progress)
        {
            Settings c = Store.Cfg;
            if (c.OptCursor) SwapCursors(dir);
            if (c.OptFont) SwapFont(dir);
            if (c.OptMono) MonoUi(dir, progress);
            File.WriteAllText(Path.Combine(dir, "FleetOpt.txt"), Signature());
        }

        // ---------------------------------------------------------------- cursor
        static readonly string[] Cursors = { "ArrowCursor.png", "ArrowFarCursor.png", "IBeamCursor.png" };

        static void SwapCursors(string dir)
        {
            string folder = Path.Combine(dir, "content", "textures", "Cursors", "KeyboardMouse");
            if (!Directory.Exists(folder)) return;
            foreach (string name in Cursors)
            {
                string f = Path.Combine(folder, name);
                if (!File.Exists(f)) continue;
                Keep(dir, f);
                string orig = Path.Combine(BackupDir(dir), "files", Rel(dir, f));
                Bitmap made;
                using (Bitmap src = LoadCopy(orig))
                    made = name.StartsWith("IBeam") ? MakeIBeam(src) : MakeArrow(src);
                using (made) made.Save(f, ImageFormat.Png);
            }
        }

        static Bitmap LoadCopy(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (Image img = Image.FromStream(fs))
            {
                Bitmap b = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(b)) g.DrawImage(img, 0, 0, img.Width, img.Height);
                return b;
            }
        }

        // where the old picture actually has pixels
        static Rectangle Bounds(Bitmap b)
        {
            int x0 = b.Width, y0 = b.Height, x1 = -1, y1 = -1;
            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                    if (b.GetPixel(x, y).A > 40) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            if (x1 < 0) return new Rectangle(b.Width / 2, b.Height / 2, 1, 1);
            return new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        // the classic Windows pointer: white with a black edge, tip where the old cursor's tip was
        static Bitmap MakeArrow(Bitmap old)
        {
            Rectangle bb = Bounds(old);
            int tipX = bb.X, tipY = bb.Y;
            for (int x = bb.X; x < bb.Right; x++) if (old.GetPixel(x, bb.Y).A > 40) { tipX = x; break; }
            float s = Math.Max(1.1f, Math.Min(3f, Math.Max(bb.Height, 20) / 20f));
            PointF[] pts = { new PointF(0, 0), new PointF(0, 16.5f), new PointF(3.8f, 12.8f), new PointF(6.6f, 19f), new PointF(9.4f, 17.8f), new PointF(6.7f, 11.6f), new PointF(11.8f, 11.6f) };
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(tipX + 0.5f + pts[i].X * s, tipY + 0.5f + pts[i].Y * s);
            Bitmap b = new Bitmap(old.Width, old.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush w = new SolidBrush(Color.White)) g.FillPolygon(w, pts);
                using (Pen p = new Pen(Color.Black, Math.Max(1.2f, s * 0.9f))) { p.LineJoin = LineJoin.Round; g.DrawPolygon(p, pts); }
            }
            return b;
        }

        static Bitmap MakeIBeam(Bitmap old)
        {
            Rectangle bb = Bounds(old);
            float h = Math.Max(18, bb.Height), cx = bb.X + bb.Width / 2f, top = bb.Y + bb.Height / 2f - h / 2f;
            float half = Math.Max(3.5f, h * 0.17f);
            Bitmap b = new Bitmap(old.Width, old.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float t = Math.Max(1.4f, h / 14f);
                using (Pen white = new Pen(Color.White, t + 2.4f)) using (Pen black = new Pen(Color.Black, t))
                    foreach (Pen p in new Pen[] { white, black })
                    {
                        g.DrawLine(p, cx, top, cx, top + h);
                        g.DrawLine(p, cx - half, top, cx + half, top);
                        g.DrawLine(p, cx - half, top + h, cx + half, top + h);
                    }
            }
            return b;
        }

        // ---------------------------------------------------------------- font
        public class FontChoice { public string Name, Regular, Bold; }

        public static List<FontChoice> FontList()
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            string[][] all =
            {
                new string[] { "Segoe UI", "segoeui.ttf", "segoeuib.ttf" },
                new string[] { "Bahnschrift", "bahnschrift.ttf", "bahnschrift.ttf" },
                new string[] { "Calibri", "calibri.ttf", "calibrib.ttf" },
                new string[] { "Candara", "candara.ttf", "candarab.ttf" },
                new string[] { "Corbel", "corbel.ttf", "corbelb.ttf" },
                new string[] { "Arial", "arial.ttf", "arialbd.ttf" },
                new string[] { "Verdana", "verdana.ttf", "verdanab.ttf" },
                new string[] { "Tahoma", "tahoma.ttf", "tahomabd.ttf" },
                new string[] { "Trebuchet MS", "trebuc.ttf", "trebucbd.ttf" },
                new string[] { "Consolas", "consola.ttf", "consolab.ttf" }
            };
            List<FontChoice> list = new List<FontChoice>();
            foreach (string[] a in all)
                if (File.Exists(Path.Combine(dir, a[1]))) list.Add(new FontChoice { Name = a[0], Regular = Path.Combine(dir, a[1]), Bold = File.Exists(Path.Combine(dir, a[2])) ? Path.Combine(dir, a[2]) : Path.Combine(dir, a[1]) });
            return list;
        }

        static void SwapFont(string dir)
        {
            Settings c = Store.Cfg;
            string regular = null, bold = null;
            if (!string.IsNullOrEmpty(c.OptFontFile) && File.Exists(c.OptFontFile)) { regular = c.OptFontFile; bold = c.OptFontFile; }
            else
            {
                FontChoice fc = FontList().FirstOrDefault(x => x.Name == c.OptFontName) ?? FontList().FirstOrDefault();
                if (fc == null) return;
                regular = fc.Regular; bold = fc.Bold;
            }
            JavaScriptSerializer js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            foreach (string fontRoot in new string[] { Path.Combine(dir, "content", "fonts"), Path.Combine(dir, "PlatformContent", "pc", "fonts") })
            {
                string fam = Path.Combine(fontRoot, "families");
                if (!Directory.Exists(fam)) continue;
                string regDst = Path.Combine(fontRoot, "FleetFont.ttf"), boldDst = Path.Combine(fontRoot, "FleetFontBold.ttf");
                Keep(dir, regDst); Keep(dir, boldDst);
                File.Copy(regular, regDst, true);
                File.Copy(bold, boldDst, true);
                foreach (string j in Directory.GetFiles(fam, "*.json"))
                {
                    try
                    {
                        Keep(dir, j);
                        string orig = Path.Combine(BackupDir(dir), "files", Rel(dir, j));
                        IDictionary root = js.DeserializeObject(File.ReadAllText(orig)) as IDictionary;
                        if (root == null || !(root["faces"] is IList)) continue;
                        foreach (object o in (IList)root["faces"])
                        {
                            IDictionary face = o as IDictionary;
                            if (face == null) continue;
                            int weight = 400;
                            try { weight = Convert.ToInt32(face["weight"]); } catch (Exception) { }
                            face["assetId"] = "rbxasset://fonts/" + (weight >= 600 ? "FleetFontBold.ttf" : "FleetFont.ttf");
                        }
                        File.WriteAllText(j, js.Serialize(root));
                    }
                    catch (Exception) { }
                }
            }
        }

        // ---------------------------------------------------------------- black and white
        static int Group(string rel)
        {
            string r = rel.ToLowerInvariant();
            if (r.Contains("emote")) return 1;
            if (r.Contains("ingamemenu") || r.Contains("settings") || r.Contains("menu")) return 0;
            if (r.Contains("topbar") || r.Contains("chat") || r.Contains("playerlist") || r.Contains("leaderboard") || r.Contains("health") || r.Contains("backpack") || r.Contains("hotbar") || r.Contains("hud") || r.Contains("notification")) return 2;
            return 3;
        }

        static void MonoUi(string dir, bool progress)
        {
            Settings c = Store.Cfg;
            bool[] on = { c.OptMonoMenu, c.OptMonoEmotes, c.OptMonoHud, c.OptMonoOther };
            List<string> files = new List<string>();
            foreach (string baseDir in new string[] { Path.Combine(dir, "content", "textures", "ui"), Path.Combine(dir, "ExtraContent", "textures", "ui"), Path.Combine(dir, "PlatformContent", "pc", "textures", "ui") })
            {
                if (!Directory.Exists(baseDir)) continue;
                foreach (string f in Directory.GetFiles(baseDir, "*.png", SearchOption.AllDirectories))
                    if (on[Group(Rel(baseDir, f))]) files.Add(f);
            }
            int n = 0;
            foreach (string f in files)
            {
                n++;
                if (progress && n % 25 == 0) { Status = "Black and white: " + n + "/" + files.Count; Progress = n / (float)Math.Max(1, files.Count); }
                try
                {
                    Keep(dir, f);
                    string orig = Path.Combine(BackupDir(dir), "files", Rel(dir, f));
                    using (Bitmap b = LoadCopy(orig))
                    {
                        Gray(b);
                        b.Save(f, ImageFormat.Png);
                    }
                }
                catch (Exception) { }
            }
        }

        static void Gray(Bitmap b)
        {
            BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int bytes = Math.Abs(d.Stride) * b.Height;
                byte[] px = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(d.Scan0, px, 0, bytes);
                for (int i = 0; i + 3 < bytes; i += 4)
                {
                    byte g = (byte)((px[i] * 114 + px[i + 1] * 587 + px[i + 2] * 299) / 1000);
                    px[i] = g; px[i + 1] = g; px[i + 2] = g;
                }
                System.Runtime.InteropServices.Marshal.Copy(px, 0, d.Scan0, bytes);
            }
            finally { b.UnlockBits(d); }
        }

        // ---------------------------------------------------------------- cleaning
        static long DirSize(string dir)
        {
            try { return new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); } catch (Exception) { return 0; }
        }

        static long Empty(string dir)
        {
            long freed = 0;
            if (!Directory.Exists(dir)) return 0;
            foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { long len = new FileInfo(f).Length; File.Delete(f); freed += len; } catch (Exception) { }
            }
            return freed;
        }

        static long Clean(StringBuilder report)
        {
            Settings c = Store.Cfg;
            long freed = 0;
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (c.OptCleanOld)
            {
                if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0) report.Append("Roblox is running, so old versions were kept. ");
                else
                    foreach (string root in Engine.RobloxRoots())
                    {
                        List<string> vs = Directory.GetDirectories(root).Where(d => File.Exists(Path.Combine(d, "RobloxPlayerBeta.exe"))).OrderByDescending(d => File.GetLastWriteTime(Path.Combine(d, "RobloxPlayerBeta.exe"))).ToList();
                        foreach (string old in vs.Skip(1))
                        {
                            long size = DirSize(old);
                            try { Directory.Delete(old, true); freed += size; }
                            catch (Exception) { report.Append("Could not remove " + Path.GetFileName(old) + ". "); }
                        }
                    }
            }
            if (c.OptCleanLogs)
            {
                freed += Empty(Path.Combine(local, "Roblox", "logs"));
                freed += Empty(Path.Combine(local, "Roblox", "Downloads"));
                freed += Empty(Path.Combine(local, "Temp", "Roblox"));
            }
            if (c.OptCleanCache)
            {
                freed += Empty(Path.Combine(local, "Roblox", "rbx-storage"));
                freed += Empty(Path.Combine(local, "Roblox", "http"));
            }
            return freed;
        }
    }
}
