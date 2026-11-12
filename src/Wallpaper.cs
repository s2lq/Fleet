using System;
using System.Collections.Generic;
using System.Drawing;

namespace Fleet
{

    public class Wallpaper
    {
        readonly List<Dot> dots = new List<Dot>();
        readonly Random rnd = new Random(11);
        float lastT = -1f;
        Rectangle lastGlow = Rectangle.Empty;
        const float Reach = 210f, Link = 150f;

        // positions are 0..1 so a resize keeps the layout. speeds are in pixels per second
        class Dot { public float X, Y, Vx, Vy, R, Glow; public Rectangle Last; }

        public PointF Mouse = new PointF(-9999, -9999);

        public bool Animated { get { return Store.Cfg.Wallpaper == "Grid" && !Motion.Minimal; } }

        // the dots only need the little patches around them repainted
        public bool Partial { get { return Store.Cfg.Wallpaper == "Dots"; } }

        public void Draw(Graphics g, int w, int h, float t)
        {
            g.Clear(Pal.Bg);
            if (Motion.Minimal) t = 0;
            if (Store.Cfg.Wallpaper == "Dots") { Fill(w, h); DrawDots(g, w, h, 1f); }
            else if (Store.Cfg.Wallpaper == "Grid") DrawGrid(g, w, h, t);
        }

        // a small copy of the dots (and the lines near the cursor) that gets stretched up behind glass panels
        public void DrawBlurSource(Graphics g, int w, int h)
        {
            g.Clear(Color.Transparent);
            if (Store.Cfg.Wallpaper == "Dots") DrawDots(g, w, h, 2.6f);
            else if (Store.Cfg.Wallpaper == "Grid") DrawGrid(g, w, h, 0f);
        }

        void Fill(int w, int h)
        {
            int want = Math.Max(60, Math.Min(170, (int)((long)w * h / 8500)));
            while (dots.Count < want)
            {
                double a = rnd.NextDouble() * Math.PI * 2;
                float sp = 6f + (float)rnd.NextDouble() * 14f;
                dots.Add(new Dot { X = (float)rnd.NextDouble(), Y = (float)rnd.NextDouble(), Vx = (float)Math.Cos(a) * sp, Vy = (float)Math.Sin(a) * sp, R = 1.4f + (float)rnd.NextDouble() * 1.6f });
            }
            if (dots.Count > want) dots.RemoveRange(want, dots.Count - want);
        }

        static Rectangle Box(float cx, float cy, float rad, float scale)
        {
            return new Rectangle((int)((cx - rad) * scale) - 1, (int)((cy - rad) * scale) - 1, (int)(rad * 2 * scale) + 3, (int)(rad * 2 * scale) + 3);
        }

        // moves everything on by the time that passed. Returns the patches that changed (in pixels), or null if the whole window should be redrawn.
        public Region Step(int w, int h, float scale, float t, PointF mouse)
        {
            Fill(w, h);
            Mouse = mouse;
            float dt = lastT < 0 ? 0f : Math.Min(0.1f, Math.Max(0f, t - lastT));
            lastT = t;
            bool first = dt <= 0f;
            Region reg = new Region();
            reg.MakeEmpty();
            bool glowing = false;
            foreach (Dot d in dots)
            {
                d.X += d.Vx * dt / w; d.Y += d.Vy * dt / h;
                if (d.X < -0.02f) d.X += 1.04f; else if (d.X > 1.02f) d.X -= 1.04f;
                if (d.Y < -0.02f) d.Y += 1.04f; else if (d.Y > 1.02f) d.Y -= 1.04f;
                float px = d.X * w, py = d.Y * h;
                float dx = px - mouse.X, dy = py - mouse.Y;
                float near = (float)Math.Sqrt(dx * dx + dy * dy);
                float want = near < Reach ? 1f - near / Reach : 0f;
                // ease toward the target so lines fade in and out instead of popping
                d.Glow += (want - d.Glow) * Math.Min(1f, dt * 9f);
                if (Math.Abs(want - d.Glow) < 0.003f) d.Glow = want;
                if (d.Glow > 0.01f) glowing = true;
                Rectangle nr = Box(px, py, d.R + 2.5f, scale);
                if (!d.Last.IsEmpty) reg.Union(d.Last);
                reg.Union(nr);
                d.Last = nr;
            }
            if (glowing || !lastGlow.IsEmpty)
            {
                Rectangle now = glowing ? Box(mouse.X, mouse.Y, Reach + 4, scale) : Rectangle.Empty;
                if (!lastGlow.IsEmpty) reg.Union(lastGlow);
                if (!now.IsEmpty) reg.Union(now);
                lastGlow = now;
            }
            if (first) { reg.Dispose(); return null; }
            return reg;
        }

        void DrawDots(Graphics g, int w, int h, float lineW)
        {
            int n = dots.Count;
            if (n == 0) return;
            float[] px = new float[n], py = new float[n];
            for (int i = 0; i < n; i++) { px[i] = dots[i].X * w; py[i] = dots[i].Y * h; }

            using (Pen pen = new Pen(Color.White, lineW))
            {
                for (int i = 0; i < n; i++)
                {
                    if (dots[i].Glow <= 0.02f) continue;
                    pen.Color = Pal.Tint((int)(170 * dots[i].Glow));
                    g.DrawLine(pen, px[i], py[i], Mouse.X, Mouse.Y);
                    for (int j = i + 1; j < n; j++)
                    {
                        if (dots[j].Glow <= 0.02f) continue;
                        float dx = px[i] - px[j], dy = py[i] - py[j];
                        float d2 = dx * dx + dy * dy;
                        if (d2 > Link * Link) continue;
                        float k = 1f - (float)Math.Sqrt(d2) / Link;
                        pen.Color = Pal.Tint((int)(150 * k * Math.Min(dots[i].Glow, dots[j].Glow) * 1.6f));
                        g.DrawLine(pen, px[i], py[i], px[j], py[j]);
                    }
                }
            }
            for (int i = 0; i < n; i++)
            {
                Dot d = dots[i];
                float r = (d.R + d.Glow * 0.9f) * (lineW > 1f ? 1.6f : 1f);
                using (SolidBrush b = new SolidBrush(Pal.Tint((int)(60 + 170 * d.Glow))))
                    g.FillEllipse(b, px[i] - r, py[i] - r, r * 2, r * 2);
            }
        }

        void DrawGrid(Graphics g, int w, int h, float t)
        {
            float horizon = h * 0.42f;
            float vx = w / 2f;
            using (Pen pen = new Pen(Pal.Text, 1f))
            {
                for (int i = -18; i <= 18; i++)
                {
                    pen.Color = Pal.Tint(22);
                    g.DrawLine(pen, vx + i * 12, horizon, vx + i * w * 0.18f, h);
                }
                float phase = (t * 0.18f) % 1f;
                for (int k = 0; k < 14; k++)
                {
                    float z = (k + phase) / 14f;
                    float y = horizon + (h - horizon) * z * z;
                    pen.Color = Pal.Tint((int)(40 * z));
                    g.DrawLine(pen, 0, y, w, y);
                }
            }
            using (System.Drawing.Drawing2D.LinearGradientBrush fade = new System.Drawing.Drawing2D.LinearGradientBrush(new RectangleF(0, horizon - 1, w, 120), Pal.Bg, Color.FromArgb(0, Pal.Bg.R, Pal.Bg.G, Pal.Bg.B), 90f))
                g.FillRectangle(fade, 0, horizon - 1, w, 120);
            using (SolidBrush top = new SolidBrush(Pal.Bg)) g.FillRectangle(top, 0, 0, w, horizon);
        }
    }
}
