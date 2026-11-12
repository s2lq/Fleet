using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Fleet
{
    // the window buttons in the top right corner and the part of the top bar that drags the window
    public partial class App
    {
        readonly List<RectangleF> holes = new List<RectangleF>();
        RectangleF captionBox;
        bool introLogoHidden;

        public bool InDragArea(PointF p)
        {
            if (p.Y < 0 || p.Y > 76) return false;
            if (captionBox.Contains(p)) return false;
            foreach (RectangleF h in holes) if (h.Contains(p)) return false;
            return true;
        }

        void DrawCaption(Ui ui)
        {
            Form f = canvas.FindForm();
            bool max = f != null && f.WindowState == FormWindowState.Maximized;
            float bw = 46, bh = 34;
            captionBox = new RectangleF(ui.W - bw * 3, 0, bw * 3, bh);
            for (int i = 0; i < 3; i++)
            {
                RectangleF r = new RectangleF(captionBox.X + i * bw, 0, bw, bh);
                bool over = ui.Over(r);
                float hot = ui.Anim("cap" + i, over ? 1f : 0f, 0.35f);
                bool close = i == 2;
                if (hot > 0.01f)
                {
                    Color bg = close ? Color.FromArgb((int)(255 * hot), 196, 43, 28) : Pal.Tint((int)(22 * hot));
                    ui.Fill(r, 0, bg);
                }
                Color ink = close && hot > 0.5f ? Color.White : Pal.Mix(Pal.Muted, Pal.Text, hot);
                float cx = r.X + bw / 2, cy = r.Y + bh / 2;
                using (Pen pen = new Pen(ink, 1.2f))
                {
                    ui.G.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    if (i == 0) ui.G.DrawLine(pen, cx - 5, cy, cx + 5, cy);
                    else if (i == 1)
                    {
                        if (!max) ui.G.DrawRectangle(pen, cx - 5, cy - 5, 10, 10);
                        else { ui.G.DrawRectangle(pen, cx - 5, cy - 2, 8, 8); ui.G.DrawLines(pen, new PointF[] { new PointF(cx - 2, cy - 2), new PointF(cx - 2, cy - 5), new PointF(cx + 5, cy - 5), new PointF(cx + 5, cy + 2), new PointF(cx + 3, cy + 2) }); }
                    }
                    ui.G.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    if (close) { ui.G.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5); ui.G.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5); }
                }
                if (over) ui.WantHand = false;
                if (ui.Pressed && over) ui.Active = "cap" + i;
                if (ui.Released && ui.Active == "cap" + i)
                {
                    ui.Active = null;
                    if (over)
                    {
                        int which = i;
                        canvas.Post(delegate { MainForm mf = canvas.FindForm() as MainForm; if (mf != null) mf.CaptionAction(which); });
                    }
                }
            }
        }
    }
}
