using System;
using System.Collections.Generic;
using System.Drawing;

namespace Fleet
{

    public class Wallpaper
    {
        readonly List<Dot> dots = new List<Dot>();
        readonly Random rnd = new Random(11);

        class Dot { public float X, Y, Speed, R, Phase; }

        public bool Animated { get { return Store.Cfg.Wallpaper != "Off" && !Motion.Minimal; } }

        public void Draw(Graphics g, int w, int h, float t)
        {
            g.Clear(Pal.Bg);
            if (Motion.Minimal) t = 0;
            if (Store.Cfg.Wallpaper == "Dots") DrawDots(g, w, h, t);
            else if (Store.Cfg.Wallpaper == "Grid") DrawGrid(g, w, h, t);
        }

        void DrawDots(Graphics g, int w, int h, float t)
        {
            if (dots.Count == 0)
                for (int i = 0; i < 42; i++)
                    dots.Add(new Dot { X = (float)rnd.NextDouble(), Y = (float)rnd.NextDouble(), Speed = 0.004f + (float)rnd.NextDouble() * 0.01f, R = 0.7f + (float)rnd.NextDouble() * 1.1f, Phase = (float)rnd.NextDouble() * 6.28f });
            foreach (Dot d in dots)
            {
                // wrap around the top
                float y = ((d.Y - d.Speed * t) % 1f + 1f) % 1f;
                float tw = 0.5f + 0.5f * (float)Math.Sin(t * 0.9 + d.Phase);
                using (SolidBrush b = new SolidBrush(Pal.Tint((int)(20 + 55 * tw))))
                    g.FillEllipse(b, d.X * w - d.R, y * h - d.R, d.R * 2, d.R * 2);
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
