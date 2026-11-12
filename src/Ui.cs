using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        public static readonly Color Good = Color.FromArgb(52, 211, 153);
        public static readonly Color Bad = Color.FromArgb(248, 113, 113);
        public static readonly Color Warn = Color.FromArgb(251, 191, 36);

        public class AccentDef { public string Name; public Color A, B; }
        public static readonly AccentDef[] Accents = new AccentDef[]
        {
            Def("White",  235, 235, 235),
            Def("Blue",   90, 150, 255),
            Def("Green",  74, 210, 130),
            Def("Red",    240, 90, 90),
            Def("Amber",  245, 180, 60),
            Def("Violet", 160, 130, 255)
        };
        static AccentDef Def(string n, int r, int g, int b) { Color c = Color.FromArgb(r, g, b); return new AccentDef { Name = n, A = c, B = c }; }
        public static AccentDef Cur { get { return Accents[Math.Max(0, Math.Min(Accents.Length - 1, Store.Cfg.Accent))]; } }
        public static Color A { get { return Cur.A; } }
        public static Color B { get { return Cur.B; } }
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

    public class ScrollState { public float Pos, Target; }

    public enum Btn { Primary, Ghost, Danger, Quiet }

    public class Ui
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
        public bool NeedFrames, WantHand;
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
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            local = Mouse; clipHit = true; NeedFrames = false; WantHand = false;
        }

        public void EndFrame()
        {
            if (Released) Active = null;
            Pressed = false; Released = false; Wheel = 0; Typed = ""; KeyHits.Clear();
        }

        public void Set(string id, float v) { anim[id] = v; }
        public bool Over(RectangleF r) { return clipHit && r.Contains(local) && !(pickId != null && pickBox.Contains(Mouse)); }

        public float Anim(string id, float target, float speed)
        {
            float v;
            if (!anim.TryGetValue(id, out v)) v = target;
            if (Math.Abs(target - v) > 0.004f) { v += (target - v) * speed; NeedFrames = true; }
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

        public void Glass(RectangleF r, float rad, float lift)
        {
            rad = Math.Min(rad, 12);
            Fill(r, rad, Pal.Mix(Pal.Panel, Pal.Panel2, lift));
            Stroke(r, rad, Pal.Mix(Pal.Line, Pal.Line2, lift), 1f);
        }

        void GlassOld(RectangleF r, float rad, float lift)
        {
            Fill(r, rad, Color.FromArgb((int)(11 + lift * 12), 255, 255, 255));
            Stroke(r, rad, Color.FromArgb((int)(24 + lift * 22), 255, 255, 255), 1f);
            RectangleF hl = new RectangleF(r.X + rad, r.Y + 0.5f, Math.Max(1, r.Width - rad * 2), 1);
            using (LinearGradientBrush br = new LinearGradientBrush(hl, Color.FromArgb(0, 255, 255, 255), Color.FromArgb(46, 255, 255, 255), 0f))
            {
                ColorBlend cb = new ColorBlend(3);
                cb.Colors = new Color[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(54, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) };
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
            Fill(r, r.Width * 0.22f, Color.FromArgb(240, 240, 240));
            float s = r.Width;
            float[,] p = { { .30f, .22f }, { .72f, .22f }, { .72f, .34f }, { .43f, .34f }, { .43f, .47f }, { .66f, .47f }, { .66f, .58f }, { .43f, .58f }, { .43f, .78f }, { .30f, .78f } };
            PointF[] v = new PointF[10];
            for (int i = 0; i < 10; i++) v[i] = new PointF(r.X + s * p[i, 0], r.Y + s * p[i, 1]);
            using (SolidBrush b = new SolidBrush(Color.Black)) G.FillPolygon(b, v);
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
                    Fill(rr, 8, Pal.Mix(Pal.A, Color.White, h * 0.22f));
                    fg = Pal.Panel;
                    break;
                case Btn.Danger:
                    Fill(rr, 8, Color.FromArgb((int)(14 + h * 26), Pal.Bad.R, Pal.Bad.G, Pal.Bad.B));
                    Stroke(rr, 8, Color.FromArgb((int)(70 + h * 60), Pal.Bad.R, Pal.Bad.G, Pal.Bad.B), 1f);
                    fg = Pal.Bad;
                    break;
                case Btn.Quiet:
                    if (h > 0.01f) Fill(rr, 8, Color.FromArgb((int)(h * 22), 255, 255, 255));
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
            Fill(track, 2.5f, Color.FromArgb(36, 255, 255, 255));
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

        public string TextField(string id, RectangleF r, string value, string hint)
        {
            bool focus = Focus == id;
            bool over = Over(r);
            if (over) WantHand = true;
            float f = Anim(id + ".f", focus);
            Fill(r, 9, Pal.Panel);
            Stroke(r, 9, focus ? Pal.Alpha(Pal.A, 200) : Pal.Line, 1f);
            if (Pressed) { if (over) { Focus = id; focus = true; } else if (Focus == id) { Focus = null; focus = false; } }
            string v = value ?? "";
            if (focus)
            {
                foreach (char ch in Typed)
                {
                    if (ch == '\b') { if (v.Length > 0) v = v.Substring(0, v.Length - 1); }
                    else if (ch >= ' ' && ch != 127) v += ch;
                }
                if (KeyHits.Contains(Keys.Enter) || KeyHits.Contains(Keys.Escape)) Focus = null;
                NeedFrames = true;
            }
            RectangleF inner = new RectangleF(r.X + 14, r.Y, r.Width - 28, r.Height);
            if (v.Length == 0 && !focus) TextIn(hint, inner, Fonts.Body, Pal.Faint, StringAlignment.Near);
            else
            {
                string shown = v;
                while (shown.Length > 1 && Measure(shown, Fonts.Body).Width > inner.Width - 4) shown = shown.Substring(1);
                TextIn(shown, inner, Fonts.Body, Pal.Text, StringAlignment.Near);
                // blinking caret
                if (focus && ((int)(Time * 2)) % 2 == 0)
                {
                    float cx = inner.X + Measure(shown, Fonts.Body).Width + 1;
                    G.DrawLine(Pens.White, cx, r.Y + 9, cx, r.Bottom - 9);
                }
            }
            return v;
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
                TextIn(ini, r, Fonts.Get(r.Width * 0.42f, true), Color.FromArgb(200, 200, 200), StringAlignment.Center);
            }
            using (Pen p = new Pen(Color.FromArgb(40, 255, 255, 255), 1f)) G.DrawEllipse(p, r);
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
            if (Pressed && over && pickId != id) { pickId = id; pickFrom = r; pickItems = items; pickScroll = 0; Pressed = false; }
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
            Fill(new RectangleF(pickBox.X - 1, pickBox.Y - 1, pickBox.Width + 2, pickBox.Height + 2), 10, Color.Black);
            Fill(pickBox, 9, Pal.Panel2);
            Stroke(pickBox, 9, Pal.Line2, 1f);
            if (pickBox.Contains(Mouse) && Math.Abs(Wheel) > 0.1f) pickScroll = Math.Max(0, Math.Min(Math.Max(0, pickItems.Length - rows), pickScroll - Wheel / 120f));
            int first = (int)pickScroll;
            for (int i = 0; i < rows && first + i < pickItems.Length; i++)
            {
                RectangleF row = new RectangleF(pickBox.X + 4, pickBox.Y + 4 + i * rh, pickBox.Width - 8, rh);
                bool hot = row.Contains(Mouse);
                if (hot) { Fill(row, 7, Color.FromArgb(24, 255, 255, 255)); WantHand = true; }
                TextIn(Fit(this, Lang.T(pickItems[first + i]), Fonts.Body, row.Width - 20), new RectangleF(row.X + 10, row.Y, row.Width - 20, row.Height), Fonts.Body, Pal.Text, StringAlignment.Near);
                if (Pressed && hot) { pickResult = first + i; pickResultId = pickId; }
            }
            if (Pressed) { pickId = null; pickBox = RectangleF.Empty; }
            NeedFrames = true;
        }

        GraphicsState scrollState;
        RectangleF scrollView;
        float scrollContent;

        public void BeginScroll(RectangleF view, float contentH, ScrollState s)
        {
            float max = Math.Max(0, contentH - view.Height);
            if (Over(view) && Math.Abs(Wheel) > 0.1f) s.Target -= Wheel * 0.6f;
            s.Target = Math.Max(0, Math.Min(max, s.Target));
            s.Pos += (s.Target - s.Pos) * 0.3f;
            if (Math.Abs(s.Target - s.Pos) > 0.3f) NeedFrames = true; else s.Pos = s.Target;
            scrollState = G.Save();
            scrollView = view; scrollContent = contentH;
            G.SetClip(view, CombineMode.Intersect);
            G.TranslateTransform(0, -s.Pos);
            // mouse needs the scroll offset too or clicks land on the wrong card
            local = new PointF(Mouse.X, Mouse.Y + s.Pos);
            clipHit = view.Contains(Mouse);
            scrollPos = s.Pos;
        }
        float scrollPos;

        public void EndScroll()
        {
            G.Restore(scrollState);
            local = Mouse; clipHit = true;
            float max = scrollContent - scrollView.Height;
            if (max > 1)
            {
                float th = Math.Max(30, scrollView.Height * scrollView.Height / scrollContent);
                float ty = scrollView.Y + (scrollView.Height - th) * (scrollPos / max);
                Fill(new RectangleF(scrollView.Right - 6, ty, 3, th), 1.5f, Color.FromArgb(70, 255, 255, 255));
            }
        }
    }
}
