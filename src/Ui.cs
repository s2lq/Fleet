using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Fleet
{

    public static class Pal
    {
        public static Color Bg = Color.FromArgb(0, 0, 0);
        public static Color Panel = Color.FromArgb(11, 11, 11);
        public static Color Panel2 = Color.FromArgb(22, 22, 22);
        public static Color Line = Color.FromArgb(34, 34, 34);
        public static Color Line2 = Color.FromArgb(70, 70, 70);
        public static Color Text = Color.FromArgb(240, 240, 240);
        public static Color Muted = Color.FromArgb(150, 150, 150);
        public static Color Faint = Color.FromArgb(94, 94, 94);
        public static Color Good = Color.FromArgb(52, 211, 153);
        public static Color Bad = Color.FromArgb(248, 113, 113);
        public static Color Warn = Color.FromArgb(251, 191, 36);
        public static bool IsLight;
        public static Color Tint(int a) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), Text.R, Text.G, Text.B); }
        static float Lum(Color c) { return (0.299f * c.R + 0.587f * c.G + 0.114f * c.B) / 255f; }
        public static bool LightColor(Color c) { return Lum(c) > 0.6f; }
        public static Color OnAccent { get { return LightColor(A) ? Color.FromArgb(8, 8, 8) : Color.White; } }

        public class AccentDef { public string Name; public Color A, B; }
        public static readonly AccentDef[] Accents = new AccentDef[]
        {
            Def("Auto",   235, 235, 235),
            Def("Blue",   90, 150, 255),
            Def("Green",  74, 210, 130),
            Def("Red",    240, 90, 90),
            Def("Amber",  245, 180, 60),
            Def("Violet", 160, 130, 255)
        };
        static AccentDef Def(string n, int r, int g, int b) { Color c = Color.FromArgb(r, g, b); return new AccentDef { Name = n, A = c, B = c }; }
        public static AccentDef Cur { get { return Accents[Math.Max(0, Math.Min(Accents.Length - 1, Store.Cfg.Accent))]; } }
        public static Color A { get { return Store.Cfg.Accent == 0 ? Text : Cur.A; } }
        public static Color B { get { return A; } }
        public static Color Alpha(Color c, int a) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B); }
        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }

    public static class Fonts
    {
        static string family;
        static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();

        static string Family()
        {
            if (family != null) return family;
            string[] want = { "Segoe UI Variable Text", "Segoe UI", "Inter", "Helvetica Neue", "DejaVu Sans" };
            using (InstalledFontCollection fc = new InstalledFontCollection())
                foreach (string w in want)
                    foreach (FontFamily f in fc.Families)
                        if (f.Name == w) { family = w; return family; }
            family = "Arial";
            return family;
        }

        public static Font Get(float size, bool bold)
        {
            string key = size + (bold ? "b" : "r");
            Font f;
            if (!cache.TryGetValue(key, out f)) { f = new Font(Family(), size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); cache[key] = f; }
            return f;
        }

        public static Font Body { get { return Get(14, false); } }
        public static Font BodyB { get { return Get(14, true); } }
        public static Font Small { get { return Get(12, false); } }
        public static Font SmallB { get { return Get(12, true); } }
        public static Font H2 { get { return Get(18, true); } }
        public static Font H1 { get { return Get(30, true); } }
        public static Font Big { get { return Get(34, true); } }
    }

    // how much animation the window may use: it adapts to the setting, to the battery and to whether Fleet is in front
    public static class Motion
    {
        public static bool Minimal;     // no wallpaper movement, no pulses
        public static int FastMs = 33;  // transitions
        public static int WallMs = 100; // wallpaper
        static DateTime checkedAt = DateTime.MinValue;
        static bool onBattery;

        public static void Refresh()
        {
            if ((DateTime.UtcNow - checkedAt).TotalSeconds > 15)
            {
                checkedAt = DateTime.UtcNow;
                try { onBattery = System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline; }
                catch (Exception) { onBattery = false; }
            }
            int mode = Store.Cfg.Motion;
            Minimal = Store.Cfg.ReduceMotion || mode == 2 || (Store.Cfg.PowerSave && onBattery);
            FastMs = 16;
            WallMs = mode == 1 ? 16 : 33;
        }
    }

    public class ScrollState { public float Pos, Target, Moved = -10f; }

    public enum Btn { Primary, Ghost, Danger, Quiet }

    public partial class Ui
    {
        public Graphics G;
        public float W, H;
        public float Time;
        public PointF Mouse;
        public bool Down, Pressed, Released, Ctrl;
        public float Wheel;
        public string Typed = "";
        public List<Keys> KeyHits = new List<Keys>();
        public string Active, Focus;
        public bool NeedFrames, WantHand, WantText;
        public Bitmap Target;       // the layer being drawn, so overlays can blur what is behind them
        public struct GlassRect { public RectangleF R; public float Rad; }
        public readonly List<GlassRect> GlassRects = new List<GlassRect>();
        public float Dt = 0.016f;
        float lastTime = -1f;
        public int NeedMs;          // something that moves all the time asks for this many milliseconds between frames
        public void Want(int ms) { if (NeedMs == 0 || ms < NeedMs) NeedMs = ms; }
        PointF local;
        bool clipHit = true;
        readonly Dictionary<string, float> anim = new Dictionary<string, float>();
        static readonly StringFormat sf = MakeFormat();

        static StringFormat MakeFormat()
        {
            StringFormat f = new StringFormat(StringFormat.GenericTypographic);
            f.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
            f.Trimming = StringTrimming.EllipsisCharacter;
            return f;
        }

        public void BeginFrame(Graphics g, float w, float h)
        {
            G = g; W = w; H = h;
            GlassRects.Clear();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            local = Mouse; clipHit = true; NeedFrames = false; NeedMs = 0; WantHand = false; WantText = false; ImeValid = false;
            // animations follow the clock, not the frame count, so they look the same at any frame rate
            Dt = lastTime < 0 ? 0.016f : Math.Max(0.001f, Math.Min(0.1f, Time - lastTime));
            lastTime = Time;
        }

        public void EndFrame()
        {
            if (Released) Active = null;
            Pressed = false; Released = false; RightPressed = false; Wheel = 0; Typed = ""; KeyHits.Clear();
        }

        public void Set(string id, float v) { anim[id] = v; }

        readonly Dictionary<string, float> appear = new Dictionary<string, float>();
        public void ResetAppear() { appear.Clear(); }
        public void Forget(string key) { appear.Remove(key); }

        // 0 to 1, eased out. Items with a higher index start a little later, so a list slides in one by one.
        public float Appear(string key, int index)
        {
            float start;
            if (!appear.TryGetValue(key, out start)) { start = Time + Math.Min(index, 12) * 0.045f; appear[key] = start; }
            float t = (Time - start) / 0.38f;
            if (t >= 1f) return 1f;
            NeedFrames = true;
            if (t <= 0f) return 0f;
            float u = 1f - t;
            return 1f - u * u * u;
        }

        public void Spinner(RectangleF r, Color c)
        {
            using (Pen p = new Pen(c, 2.2f))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                G.DrawArc(p, r, (Time * 320f) % 360f, 100f);
            }
            Want(66);
        }

        // a soft ring that grows and fades, used on the "running" dots
        public void Pulse(float cx, float cy, float rad, Color c)
        {
            if (Motion.Minimal) return;
            // a short pulse, then a long rest: no frames are needed while it rests
            float cycle = Time % 4.2f;
            if (cycle > 1.3f) return;
            float t = cycle / 1.3f;
            float rr = rad + t * 9f;
            using (Pen p = new Pen(Color.FromArgb((int)((1f - t) * 110), c.R, c.G, c.B), 1.6f)) G.DrawEllipse(p, cx - rr, cy - rr, rr * 2, rr * 2);
            Want(80);
        }
        public bool Blocked;
        public static float EaseOut(float t) { t = Math.Max(0f, Math.Min(1f, t)); float u = 1f - t; return 1f - u * u * u; }
        public static float EaseInOut(float t) { t = Math.Max(0f, Math.Min(1f, t)); return t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f; }
        public static float EaseBack(float t) { t = Math.Max(0f, Math.Min(1f, t)); float c = 1.70158f, u = t - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }
        public bool Over(RectangleF r) { return !Blocked && clipHit && r.Contains(local) && !(pickId != null && pickBox.Contains(Mouse)); }

        public float Anim(string id, float target, float speed)
        {
            float v;
            if (!anim.TryGetValue(id, out v)) v = target;
            if (Math.Abs(target - v) > 0.002f) { v += (target - v) * (1f - (float)Math.Pow(1f - speed, Dt * 30f)); NeedFrames = true; }
            else v = target;
            anim[id] = v;
            return v;
        }
        public float Anim(string id, bool on) { return Anim(id, on ? 1f : 0f, 0.22f); }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public void Fill(RectangleF r, float rad, Color c)
        {
            using (GraphicsPath p = Round(r, rad)) using (SolidBrush b = new SolidBrush(c)) G.FillPath(b, p);
        }

        public void Stroke(RectangleF r, float rad, Color c, float w)
        {
            using (GraphicsPath p = Round(r, rad)) using (Pen pen = new Pen(c, w)) G.DrawPath(pen, p);
        }

        public void Gradient(RectangleF r, float rad, Color a, Color b, float angle)
        {
            if (r.Width < 2 || r.Height < 2) return;
            using (GraphicsPath p = Round(r, rad))
            using (LinearGradientBrush br = new LinearGradientBrush(r, a, b, angle)) G.FillPath(br, p);
        }

        // a see-through panel. the canvas puts a blurred copy of the wallpaper behind it
        public void Glass(RectangleF r, float rad, float lift)
        {
            rad = Math.Min(rad, 12);
            Fill(r, rad, Pal.Alpha(Pal.Mix(Pal.Panel, Pal.Panel2, lift), 165));
            Stroke(r, rad, Pal.Mix(Pal.Line, Pal.Line2, lift), 1f);
        }

        // like Glass, but for things on top of other things (popups, notifications): blurs whatever was drawn under it
        public void Frost(RectangleF r, float rad, Color tint, int alpha)
        {
            try
            {
                if (false && Target != null)
                {
                    PointF[] pts = { new PointF(r.X, r.Y), new PointF(r.Right, r.Bottom) };
                    using (Matrix m = G.Transform) m.TransformPoints(pts);
                    Rectangle px = Rectangle.Intersect(new Rectangle(0, 0, Target.Width, Target.Height), Rectangle.Round(new RectangleF(pts[0].X, pts[0].Y, pts[1].X - pts[0].X, pts[1].Y - pts[0].Y)));
                    if (px.Width > 8 && px.Height > 8)
                    {
                        using (Bitmap crop = Target.Clone(px, Target.PixelFormat))
                        using (Bitmap small = new Bitmap(Math.Max(2, px.Width / 6), Math.Max(2, px.Height / 6), PixelFormat.Format32bppPArgb))
                        {
                            using (Graphics sg = Graphics.FromImage(small)) { sg.InterpolationMode = InterpolationMode.HighQualityBilinear; sg.DrawImage(crop, 0, 0, small.Width, small.Height); }
                            GraphicsState st = G.Save();
                            using (GraphicsPath p = Round(r, rad))
                            {
                                G.SetClip(p, CombineMode.Intersect);
                                G.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                G.CompositingMode = CompositingMode.SourceCopy;
                                G.DrawImage(small, r);
                            }
                            G.Restore(st);
                        }
                    }
                }
            }
            catch (Exception) { }
            Fill(r, rad, Pal.Alpha(tint, Math.Max(alpha, 225)));
        }

        void GlassOld(RectangleF r, float rad, float lift)
        {
            Fill(r, rad, Pal.Tint((int)(11 + lift * 12)));
            Stroke(r, rad, Pal.Tint((int)(24 + lift * 22)), 1f);
            RectangleF hl = new RectangleF(r.X + rad, r.Y + 0.5f, Math.Max(1, r.Width - rad * 2), 1);
            using (LinearGradientBrush br = new LinearGradientBrush(hl, Pal.Tint(0), Pal.Tint(46), 0f))
            {
                ColorBlend cb = new ColorBlend(3);
                cb.Colors = new Color[] { Pal.Tint(0), Pal.Tint(54), Pal.Tint(0) };
                cb.Positions = new float[] { 0, 0.5f, 1 };
                br.InterpolationColors = cb;
                G.FillRectangle(br, hl);
            }
        }

        public SizeF Measure(string s, Font f)
        {
            s = Lang.T(s);
            SizeF z = G.MeasureString(s ?? "", f, 4000, sf);
            return new SizeF((float)Math.Ceiling(z.Width), (float)Math.Ceiling(z.Height));
        }

        public void Text(string s, float x, float y, Font f, Color c)
        {
            s = Lang.T(s);
            if (string.IsNullOrEmpty(s)) return;
            using (SolidBrush b = new SolidBrush(c)) G.DrawString(s, f, b, x, y, sf);
        }

        public void TextIn(string s, RectangleF r, Font f, Color c, StringAlignment h)
        {
            s = Lang.T(s);
            if (string.IsNullOrEmpty(s)) return;
            StringFormat fmt = new StringFormat(sf);
            fmt.Alignment = h; fmt.LineAlignment = StringAlignment.Center;
            using (SolidBrush b = new SolidBrush(c)) G.DrawString(s, f, b, r, fmt);
            fmt.Dispose();
        }

        public float Para(string s, RectangleF r, Font f, Color c)
        {
            s = Lang.T(s);
            StringFormat fmt = new StringFormat(StringFormat.GenericTypographic);
            fmt.Trimming = StringTrimming.Word;
            SizeF z = G.MeasureString(s, f, (int)r.Width, fmt);
            using (SolidBrush b = new SolidBrush(c)) G.DrawString(s, f, b, new RectangleF(r.X, r.Y, r.Width, z.Height + 2), fmt);
            fmt.Dispose();
            return (float)Math.Ceiling(z.Height);
        }

        public static string Fit(Ui ui, string s, Font f, float w)
        {
            if (string.IsNullOrEmpty(s) || ui.Measure(s, f).Width <= w) return s;
            while (s.Length > 1 && ui.Measure(s + "...", f).Width > w) s = s.Substring(0, s.Length - 1);
            return s + "...";
        }

        public void Icon(string name, RectangleF r, Color c, float w)
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, s = Math.Min(r.Width, r.Height) / 2;
            using (Pen p = new Pen(c, w))
            using (SolidBrush b = new SolidBrush(c))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                switch (name)
                {
                    case "info": G.DrawEllipse(p, cx - s * 0.7f, cy - s * 0.7f, s * 1.4f, s * 1.4f); G.DrawLine(p, cx, cy - s * 0.05f, cx, cy + s * 0.35f); G.FillEllipse(b, cx - s * 0.09f, cy - s * 0.4f, s * 0.18f, s * 0.18f); break;
                    case "play": G.FillPolygon(b, new PointF[] { new PointF(cx - s * 0.45f, cy - s * 0.6f), new PointF(cx + s * 0.65f, cy), new PointF(cx - s * 0.45f, cy + s * 0.6f) }); break;
                    case "stop": G.FillPath(b, Round(new RectangleF(cx - s * 0.5f, cy - s * 0.5f, s, s), 2)); break;
                    case "plus": G.DrawLine(p, cx - s * 0.6f, cy, cx + s * 0.6f, cy); G.DrawLine(p, cx, cy - s * 0.6f, cx, cy + s * 0.6f); break;
                    case "x": G.DrawLine(p, cx - s * 0.5f, cy - s * 0.5f, cx + s * 0.5f, cy + s * 0.5f); G.DrawLine(p, cx + s * 0.5f, cy - s * 0.5f, cx - s * 0.5f, cy + s * 0.5f); break;
                    case "check": G.DrawLines(p, new PointF[] { new PointF(cx - s * 0.55f, cy), new PointF(cx - s * 0.15f, cy + s * 0.45f), new PointF(cx + s * 0.6f, cy - s * 0.45f) }); break;
                    case "trash":
                        G.DrawLine(p, cx - s * 0.6f, cy - s * 0.4f, cx + s * 0.6f, cy - s * 0.4f);
                        G.DrawLines(p, new PointF[] { new PointF(cx - s * 0.45f, cy - s * 0.4f), new PointF(cx - s * 0.35f, cy + s * 0.6f), new PointF(cx + s * 0.35f, cy + s * 0.6f), new PointF(cx + s * 0.45f, cy - s * 0.4f) });
                        G.DrawLines(p, new PointF[] { new PointF(cx - s * 0.2f, cy - s * 0.4f), new PointF(cx - s * 0.2f, cy - s * 0.7f), new PointF(cx + s * 0.2f, cy - s * 0.7f), new PointF(cx + s * 0.2f, cy - s * 0.4f) });
                        break;
                    case "edit":
                        G.DrawLine(p, cx - s * 0.55f, cy + s * 0.55f, cx + s * 0.4f, cy - s * 0.4f);
                        G.DrawLine(p, cx - s * 0.6f, cy + s * 0.6f, cx - s * 0.2f, cy + s * 0.5f);
                        G.DrawLine(p, cx + s * 0.25f, cy - s * 0.55f, cx + s * 0.55f, cy - s * 0.25f);
                        break;
                    case "search": G.DrawEllipse(p, cx - s * 0.6f, cy - s * 0.6f, s * 1.0f, s * 1.0f); G.DrawLine(p, cx + s * 0.2f, cy + s * 0.2f, cx + s * 0.65f, cy + s * 0.65f); break;
                    case "refresh": G.DrawArc(p, cx - s * 0.6f, cy - s * 0.6f, s * 1.2f, s * 1.2f, 30, 280); G.FillPolygon(b, new PointF[] { new PointF(cx + s * 0.75f, cy - s * 0.1f), new PointF(cx + s * 0.35f, cy - s * 0.2f), new PointF(cx + s * 0.7f, cy - s * 0.55f) }); break;
                    case "bolt": G.FillPolygon(b, new PointF[] { new PointF(cx + s * 0.15f, cy - s * 0.8f), new PointF(cx - s * 0.5f, cy + s * 0.1f), new PointF(cx - s * 0.05f, cy + s * 0.1f), new PointF(cx - s * 0.2f, cy + s * 0.8f), new PointF(cx + s * 0.5f, cy - s * 0.15f), new PointF(cx + s * 0.05f, cy - s * 0.15f) }); break;
                    case "trim": G.DrawEllipse(p, cx - s * 0.6f, cy - s * 0.6f, s * 1.2f, s * 1.2f); G.DrawLine(p, cx - s * 0.3f, cy, cx + s * 0.3f, cy); break;
                    case "grid": for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++) G.DrawPath(p, Round(new RectangleF(cx - s * 0.65f + i * s * 0.75f, cy - s * 0.65f + j * s * 0.75f, s * 0.55f, s * 0.55f), 2)); break;
                    default: G.FillEllipse(b, cx - s * 0.3f, cy - s * 0.3f, s * 0.6f, s * 0.6f); break;
                }
            }
        }

        public void Logo(RectangleF r)
        {
            Fill(r, r.Width * 0.22f, Pal.Text);
            float s = r.Width;
            float[,] p = { { .30f, .22f }, { .72f, .22f }, { .72f, .34f }, { .43f, .34f }, { .43f, .47f }, { .66f, .47f }, { .66f, .58f }, { .43f, .58f }, { .43f, .78f }, { .30f, .78f } };
            PointF[] v = new PointF[10];
            for (int i = 0; i < 10; i++) v[i] = new PointF(r.X + s * p[i, 0], r.Y + s * p[i, 1]);
            using (SolidBrush b = new SolidBrush(Pal.Bg)) G.FillPolygon(b, v);
        }

        public bool Button(string id, RectangleF r, string label, Btn style, string icon)
        {
            bool over = Over(r);
            if (over) WantHand = true;
            float h = Anim(id + ".h", over && !(Down && Active == id) ? 1f : 0f, 0.28f);
            float press = Anim(id + ".p", Active == id && Down ? 1f : 0f, 0.4f);
            RectangleF rr = r;
            if (press > 0.01f) rr = new RectangleF(r.X + press, r.Y + press, r.Width - press * 2, r.Height - press * 2);
            Color fg = Pal.Text;
            switch (style)
            {
                case Btn.Primary:
                    Fill(rr, 8, Pal.Mix(Pal.A, Pal.IsLight ? Color.Black : Color.White, h * 0.22f));
                    fg = Pal.OnAccent;
                    break;
                case Btn.Danger:
                    Fill(rr, 8, Color.FromArgb((int)(14 + h * 26), Pal.Bad.R, Pal.Bad.G, Pal.Bad.B));
                    Stroke(rr, 8, Color.FromArgb((int)(70 + h * 60), Pal.Bad.R, Pal.Bad.G, Pal.Bad.B), 1f);
                    fg = Pal.Bad;
                    break;
                case Btn.Quiet:
                    if (h > 0.01f) Fill(rr, 8, Pal.Tint((int)(h * 22)));
                    fg = Pal.Mix(Pal.Muted, Pal.Text, h);
                    break;
                default:
                    Fill(rr, 8, Pal.Mix(Pal.Panel2, Pal.Line, h));
                    Stroke(rr, 8, Pal.Mix(Pal.Line, Pal.Line2, h), 1f);
                    break;
            }
            Font f = Fonts.BodyB;
            SizeF m = Measure(label ?? "", f);
            float iw = icon != null ? 16 : 0, gap = icon != null && !string.IsNullOrEmpty(label) ? 7 : 0;
            float x = r.X + (r.Width - (iw + gap + m.Width)) / 2;
            if (icon != null) { Icon(icon, new RectangleF(x, r.Y + (r.Height - 16) / 2, 16, 16), fg, 1.7f); x += iw + gap; }
            if (!string.IsNullOrEmpty(label)) TextIn(label, new RectangleF(x, r.Y, m.Width + 2, r.Height), f, fg, StringAlignment.Near);

            // click = pressed AND released on the same button
            if (Pressed && over) Active = id;
            if (Released && Active == id) { Active = null; return over; }
            return false;
        }

        public bool Toggle(string id, RectangleF r, bool value)
        {
            bool over = Over(r);
            if (over) WantHand = true;
            // knob slides instead of snapping
            float t = Anim(id + ".t", value);
            Fill(r, r.Height / 2, Pal.Mix(Pal.Line, Pal.A, t));
            if (t > 0.01f) Gradient(r, r.Height / 2, Pal.Alpha(Pal.A, (int)(255 * t)), Pal.Alpha(Pal.B, (int)(255 * t)), 0f);
            float k = r.Height - 6;
            float kx = r.X + 3 + (r.Width - 6 - k) * t;
            Fill(new RectangleF(kx, r.Y + 3, k, k), k / 2, Color.White);
            if (Pressed && over) Active = id;
            if (Released && Active == id) { Active = null; if (over) return !value; }
            return value;
        }

        public int Segmented(string id, RectangleF r, string[] items, int sel)
        {
            Fill(r, 9, Pal.Panel);
            Stroke(r, 9, Pal.Line, 1f);
            float cw = (r.Width - 6) / items.Length;
            float pos = Anim(id + ".s", sel, 0.25f);
            RectangleF pill = new RectangleF(r.X + 3 + pos * cw, r.Y + 3, cw, r.Height - 6);
            Fill(pill, 7, Pal.Line);
            Stroke(pill, 7, Pal.Line2, 1f);
            int result = sel;
            for (int i = 0; i < items.Length; i++)
            {
                RectangleF cell = new RectangleF(r.X + 3 + i * cw, r.Y + 3, cw, r.Height - 6);
                bool over = Over(cell);
                if (over) WantHand = true;
                TextIn(items[i], cell, Fonts.BodyB, i == sel ? Pal.Text : Pal.Mix(Pal.Muted, Pal.Text, over ? 0.6f : 0f), StringAlignment.Center);
                if (Pressed && over) Active = id + i;
                if (Released && Active == id + i) { Active = null; if (over) result = i; }
            }
            return result;
        }

        public int Slider(string id, RectangleF r, int value, int min, int max)
        {
            float t = max > min ? (value - min) / (float)(max - min) : 0;
            float ty = r.Y + r.Height / 2;
            RectangleF track = new RectangleF(r.X, ty - 2.5f, r.Width, 5);
            Fill(track, 2.5f, Pal.Tint(36));
            if (t > 0.001f) Gradient(new RectangleF(r.X, ty - 2.5f, r.Width * t, 5), 2.5f, Pal.A, Pal.B, 0f);
            bool over = Over(r);
            if (over) WantHand = true;
            float glow = Anim(id + ".g", over || Active == id);
            float kx = r.X + r.Width * t;
            Fill(new RectangleF(kx - 8, ty - 8, 16, 16), 8, Color.White);
            if (Pressed && over) Active = id;
            if (Active == id)
            {
                if (Down)
                {
                    float f = Math.Max(0, Math.Min(1, (local.X - r.X) / r.Width));
                    return (int)Math.Round(min + f * (max - min));
                }
                if (Released) Active = null;
            }
            return value;
        }

        public void Bar(RectangleF r, float frac, Color a, Color b)
        {
            Fill(r, r.Height / 2, Pal.Line);
            frac = Math.Max(0, Math.Min(1, frac));
            if (frac > 0.005f) Gradient(new RectangleF(r.X, r.Y, Math.Max(r.Height, r.Width * frac), r.Height), r.Height / 2, a, b, 0f);
        }

        public void Chip(string text, float x, float y, Color c)
        {
            SizeF m = Measure(text, Fonts.SmallB);
            RectangleF r = new RectangleF(x, y, m.Width + 20, 22);
            Fill(r, 11, Pal.Alpha(c, 28));
            TextIn(text, r, Fonts.SmallB, c, StringAlignment.Center);
        }

        public float ChipWidth(string text) { return Measure(text, Fonts.SmallB).Width + 20; }

        public void Avatar(RectangleF r, Account a)
        {
            Bitmap face = Roblox.Face(a.UserId);
            if (face != null)
            {
                GraphicsState st = G.Save();
                using (GraphicsPath p = new GraphicsPath()) { p.AddEllipse(r); G.SetClip(p, CombineMode.Intersect); }
                G.InterpolationMode = InterpolationMode.HighQualityBicubic;
                G.DrawImage(face, r);
                G.Restore(st);
            }
            else
            {
                using (GraphicsPath p = new GraphicsPath()) { p.AddEllipse(r); using (SolidBrush br = new SolidBrush(Pal.Line)) G.FillPath(br, p); }
                string ini = string.IsNullOrEmpty(a.Label) ? "?" : a.Label.Substring(0, 1).ToUpper();
                TextIn(ini, r, Fonts.Get(r.Width * 0.42f, true), Pal.Muted, StringAlignment.Center);
            }
            using (Pen p = new Pen(Pal.Tint(40), 1f)) G.DrawEllipse(p, r);
        }

        // ------------------------------------------------------------ drop-down list
        string pickId;
        RectangleF pickBox, pickFrom;
        string[] pickItems = new string[0];
        float pickScroll;
        int pickResult = -1;
        string pickResultId;

        public int Picker(string id, RectangleF r, string[] items, int sel, string hint)
        {
            bool over = Over(r);
            if (over) WantHand = true;
            float h = Anim(id + ".h", over || pickId == id);
            Fill(r, 9, Pal.Mix(Pal.Panel, Pal.Panel2, h));
            Stroke(r, 9, Pal.Mix(Pal.Line, Pal.Line2, h), 1f);
            bool has = sel >= 0 && sel < items.Length;
            string shown = has ? items[sel] : (hint ?? "");
            TextIn(Fit(this, Lang.T(shown), Fonts.Body, r.Width - 44), new RectangleF(r.X + 14, r.Y, r.Width - 40, r.Height), Fonts.Body, has ? Pal.Text : Pal.Muted, StringAlignment.Near);
            using (Pen p = new Pen(Pal.Muted, 1.6f))
            {
                float cx = r.Right - 20, cy = r.Y + r.Height / 2;
                G.DrawLines(p, new PointF[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
            }
            if (pickResultId == id && pickResult >= 0) { sel = pickResult; pickResult = -1; pickResultId = null; }
            if (Pressed && over && pickId != id) { pickId = id; pickFrom = r; pickItems = items; pickScroll = 0; Pressed = false; Set("pick.open", 0f); }
            return sel;
        }

        // call last in the frame so the list sits on top of everything
        public void Overlay()
        {
            if (pickId == null) return;
            int rows = Math.Min(8, pickItems.Length);
            float rh = 34;
            pickBox = new RectangleF(pickFrom.X, pickFrom.Bottom + 4, pickFrom.Width, rows * rh + 8);
            if (pickBox.Bottom > H - 8) pickBox = new RectangleF(pickFrom.X, pickFrom.Y - 4 - (rows * rh + 8), pickFrom.Width, rows * rh + 8);
            float open = Anim("pick.open", 1f, 0.3f);
            GraphicsState gs = G.Save();
            G.SetClip(new RectangleF(pickBox.X - 3, pickBox.Y - 3, pickBox.Width + 6, (pickBox.Height + 6) * open));
            Fill(new RectangleF(pickBox.X - 1, pickBox.Y - 1, pickBox.Width + 2, pickBox.Height + 2), 10, Pal.Line);
            Frost(pickBox, 9, Pal.Panel2, 205);
            Stroke(pickBox, 9, Pal.Line2, 1f);
            if (pickBox.Contains(Mouse) && Math.Abs(Wheel) > 0.1f) pickScroll = Math.Max(0, Math.Min(Math.Max(0, pickItems.Length - rows), pickScroll - Wheel / 120f));
            int first = (int)pickScroll;
            for (int i = 0; i < rows && first + i < pickItems.Length; i++)
            {
                RectangleF row = new RectangleF(pickBox.X + 4, pickBox.Y + 4 + i * rh, pickBox.Width - 8, rh);
                bool hot = row.Contains(Mouse);
                if (hot) { Fill(row, 7, Pal.Tint(24)); WantHand = true; }
                TextIn(Fit(this, Lang.T(pickItems[first + i]), Fonts.Body, row.Width - 20), new RectangleF(row.X + 10, row.Y, row.Width - 20, row.Height), Fonts.Body, Pal.Text, StringAlignment.Near);
                if (Pressed && hot) { pickResult = first + i; pickResultId = pickId; }
            }
            G.Restore(gs);
            if (Pressed) { pickId = null; pickBox = RectangleF.Empty; }
            NeedFrames = true;
        }

        GraphicsState scrollState;
        RectangleF scrollView;
        float scrollContent, scrollPos, dragGrab;
        ScrollState scrollRef;

        public void BeginScroll(RectangleF view, float contentH, ScrollState s)
        {
            float max = Math.Max(0, contentH - view.Height);
            if (Over(view) && Math.Abs(Wheel) > 0.1f) { s.Target -= Wheel * 0.6f; s.Moved = Time; }
            s.Target = Math.Max(0, Math.Min(max, s.Target));
            s.Pos += (s.Target - s.Pos) * 0.3f;
            if (Math.Abs(s.Target - s.Pos) > 0.3f) { NeedFrames = true; s.Moved = Time; } else s.Pos = s.Target;
            scrollState = G.Save();
            scrollView = view; scrollContent = contentH; scrollRef = s;
            G.SetClip(view, CombineMode.Intersect);
            G.TranslateTransform(0, -s.Pos);
            // mouse needs the scroll offset too or clicks land on the wrong card
            local = new PointF(Mouse.X, Mouse.Y + s.Pos);
            clipHit = view.Contains(Mouse);
            scrollPos = s.Pos;
        }

        // a slim bar that fades in when you scroll or come near it, grows when hovered, and can be dragged
        public void EndScroll()
        {
            G.Restore(scrollState);
            local = Mouse; clipHit = true;
            RectangleF v = scrollView;
            float max = scrollContent - v.Height;
            if (max <= 1 || scrollRef == null) return;
            ScrollState s = scrollRef;
            string id = "sb" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s);
            float trackY = v.Y + 6, trackH = v.Height - 12;
            float th = Math.Max(40, trackH * v.Height / scrollContent);
            float ty = trackY + (trackH - th) * (s.Pos / max);
            RectangleF zone = new RectangleF(v.Right - 18, v.Y, 18, v.Height);
            bool hot = !Blocked && zone.Contains(Mouse);
            bool dragging = Active == id;
            float grow = Anim(id + ".w", hot || dragging ? 1f : 0f, 0.3f);
            float shown = Anim(id + ".v", (hot || dragging || Time - s.Moved < 1.1f) ? 1f : 0f, 0.14f);
            if (hot) WantHand = true;
            if (Pressed && hot && !Blocked)
            {
                RectangleF thumbHit = new RectangleF(v.Right - 18, ty - 2, 18, th + 4);
                if (thumbHit.Contains(Mouse)) { Active = id; dragGrab = Mouse.Y - ty; dragging = true; }
                else { s.Target = Math.Max(0, Math.Min(max, s.Target + (Mouse.Y < ty ? -1 : 1) * v.Height * 0.9f)); s.Moved = Time; }
            }
            if (dragging && Down)
            {
                float frac = Math.Max(0, Math.Min(1, (Mouse.Y - dragGrab - trackY) / Math.Max(1, trackH - th)));
                s.Target = s.Pos = frac * max; s.Moved = Time;
                ty = trackY + (trackH - th) * frac;
            }
            if (shown < 0.02f) return;
            float w = 4f + 3f * grow;
            RectangleF bar = new RectangleF(v.Right - 8 - w, ty, w, th);
            Fill(bar, w / 2, Pal.Tint((int)((70 + 90 * grow) * shown)));
        }
    }
}
