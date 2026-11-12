using System;
using System.Drawing;
using System.Windows.Forms;

namespace Fleet
{

    public class Canvas : Control
    {
        readonly Ui ui = new Ui();
        readonly Wallpaper wallpaper = new Wallpaper();
        readonly App app;
        readonly Timer timer = new Timer();
        readonly DateTime start = DateTime.UtcNow;
        float scale = 1f;
        Bitmap layer, blurBmp;
        bool uiDirty = true;
        int lastUi, lastWall;
        public static bool AssumeFront;     // for tests on machines without a window manager
        int lastSecond = Environment.TickCount;
        bool keepFast;
        int keepMs;

        public Canvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = Pal.Bg;
            app = new App(this);
            timer.Interval = 100;
            timer.Tick += delegate { OnTimer(); };
            timer.Start();
            Log.Changed += delegate { try { if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { Redraw(); }); } catch (Exception) { } };
        }

        // The timer only runs as fast as something on screen needs. Idle, hidden or in the background it ticks once a second.
        // the interface is drawn into a cached layer that is only redrawn when something in it changes;
        // a moving wallpaper alone just re-draws the background and lays the cached layer on top
        void Redraw() { uiDirty = true; Invalidate(); }

        protected override void OnResize(EventArgs e) { uiDirty = true; base.OnResize(e); }

        void OnTimer()
        {
            int now = Environment.TickCount;
            bool second = now - lastSecond >= 1000;
            if (second) { lastSecond = now; Engine.Tick(); app.EverySecond(); }
            Motion.Refresh();
            Form f = FindForm();
            bool visible = f != null && f.Visible && f.WindowState != FormWindowState.Minimized;
            bool front = visible && (AssumeFront || Form.ActiveForm != null);
            int next = 1000;
            if (visible)
            {
                int uiNeed = keepFast ? Motion.FastMs : keepMs;
                int wallNeed = wallpaper.Animated ? (wallpaper.Partial ? Motion.WallMs : Math.Max(Motion.WallMs, 100)) : 0;
                if (front)
                {
                    if (uiNeed > 0 && now - lastUi >= uiNeed - 3) { if (wallNeed > 0) { Region r = StepWall(); if (r != null) r.Dispose(); } Redraw(); lastUi = lastWall = now; }
                    else if (wallNeed > 0 && now - lastWall >= wallNeed - 3) { WallTick(); lastWall = now; }
                    if (uiNeed > 0) next = uiNeed;
                    if (wallNeed > 0 && wallNeed < next) next = wallNeed;
                    if (next > 1000) next = 1000;
                }
                if (second) { Redraw(); lastUi = now; }
            }
            if (timer.Interval != next) timer.Interval = next;
        }

        float Now { get { return (float)(DateTime.UtcNow - start).TotalSeconds; } }

        Region StepWall()
        {
            if (!wallpaper.Partial || Width < 2 || Height < 2) return null;
            return wallpaper.Step((int)Math.Ceiling(Width / scale), (int)Math.Ceiling(Height / scale), scale, Now, ui.Mouse);
        }

        // repaint only the patches where a dot moved
        void WallTick()
        {
            Region r = StepWall();
            if (r == null) { Invalidate(); return; }
            Invalidate(r);
            r.Dispose();
        }

        public App AppRef { get { return app; } }
        public void StartUp() { app.StartUp(); }
        public void RunPreset(Preset p) { app.RunPreset(p); }
        public void ApplyShell() { MainForm f = FindForm() as MainForm; if (f != null) f.ApplyShell(); }
        public void ShowMini() { MainForm f = FindForm() as MainForm; if (f != null) f.ToggleMini(); }

        public void Navigate(string page) { app.Navigate(page); Redraw(); }

        // dialogs must not open in the middle of a paint
        public void Post(Action a) { BeginInvoke((MethodInvoker)delegate { try { a(); } catch (Exception ex) { Log.Add("Error: " + ex.Message); } Redraw(); }); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            scale = Math.Max(1f, g.DpiX / 96f);
            int w = (int)Math.Ceiling(Width / scale), h = (int)Math.Ceiling(Height / scale);
            ui.Time = (float)(DateTime.UtcNow - start).TotalSeconds;
            if (Width < 2 || Height < 2) return;
            if (layer == null || layer.Width != Width || layer.Height != Height)
            {
                if (layer != null) layer.Dispose();
                layer = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                uiDirty = true;
            }
            if (uiDirty)
            {
                uiDirty = false;
                using (Graphics lg = Graphics.FromImage(layer))
                {
                    lg.Clear(Color.Transparent);
                    lg.ScaleTransform(scale, scale);
                    ui.Target = layer;
                    ui.BeginFrame(lg, w, h);
                    try { app.Render(ui); }
                    catch (Exception ex) { Log.Add("Draw error: " + ex.Message); }
                    keepFast = ui.NeedFrames;
                    keepMs = ui.NeedMs;
                    Cursor = ui.WantText ? Cursors.IBeam : (ui.WantHand ? Cursors.Hand : Cursors.Default);
                    ui.EndFrame();
                }
            }
            if (blurBmp == null || blurBmp.Width != Math.Max(2, Width / 4) || blurBmp.Height != Math.Max(2, Height / 4))
            {
                if (blurBmp != null) blurBmp.Dispose();
                blurBmp = new Bitmap(Math.Max(2, Width / 4), Math.Max(2, Height / 4), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            }
            // wallpaper first (it respects whatever part of the window is being repainted)
            g.ResetTransform();
            g.ScaleTransform(scale, scale);
            wallpaper.Mouse = ui.Mouse;
            wallpaper.Draw(g, w, h, ui.Time);
            g.ResetTransform();
            // panels are see-through: behind each one sits a blurred copy of the wallpaper
            if (ui.GlassRects.Count > 0 && Store.Cfg.Wallpaper != "Off")
            {
                using (Graphics bg = Graphics.FromImage(blurBmp))
                {
                    bg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    bg.ScaleTransform(blurBmp.Width / (float)w, blurBmp.Height / (float)h);
                    wallpaper.DrawBlurSource(bg, w, h);
                }
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                foreach (Ui.GlassRect gr in ui.GlassRects.ToArray())
                {
                    if (!gr.R.IntersectsWith(g.ClipBounds)) continue;
                    using (System.Drawing.Drawing2D.GraphicsPath p = Ui.Round(gr.R, gr.Rad))
                    {
                        System.Drawing.Drawing2D.GraphicsState st = g.Save();
                        g.SetClip(p, System.Drawing.Drawing2D.CombineMode.Intersect);
                        g.DrawImage(blurBmp, new Rectangle(0, 0, Width, Height));
                        g.Restore(st);
                    }
                }
            }
            // the interface on top, only the repainted patches of it
            RectangleF[] scans;
            using (Region cr = g.Clip.Clone()) using (System.Drawing.Drawing2D.Matrix mx = new System.Drawing.Drawing2D.Matrix()) scans = cr.GetRegionScans(mx);
            if (scans.Length > 400) scans = new RectangleF[] { g.ClipBounds };
            foreach (RectangleF sr in scans)
            {
                Rectangle rr = Rectangle.Intersect(new Rectangle(0, 0, Width, Height), Rectangle.Ceiling(RectangleF.Inflate(sr, 1, 1)));
                if (rr.Width > 0 && rr.Height > 0) g.DrawImage(layer, rr, rr.X, rr.Y, rr.Width, rr.Height, GraphicsUnit.Pixel);
            }
            ImeUpdate();
            if (ui.ContextField != null) { string id = ui.ContextField; ui.ContextField = null; BeginInvoke((MethodInvoker)delegate { ShowFieldMenu(id); }); }
        }

        bool imeOn;

        // keep the input method on only while a text field has focus, and next to its caret
        void ImeUpdate()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || !IsHandleCreated) return;
            bool want = ui.ImeValid;
            if (want != imeOn) { imeOn = want; ImeMode = want ? ImeMode.NoControl : ImeMode.Disable; }
            if (want) Native.PlaceIme(Handle, (int)ui.ImeAt.X, (int)ui.ImeAt.Y);
        }

        // the form decides what the edges and the top bar do, so the canvas lets those clicks fall through
        public bool InDragArea(Point px) { return app.InDragArea(new PointF(px.X / scale, px.Y / scale)); }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84 && Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                Form f = FindForm();
                if (f != null && f.FormBorderStyle != FormBorderStyle.None)
                {
                    int v = (int)(long)m.LParam;
                    Point p = PointToClient(new Point((short)(v & 0xFFFF), (short)((v >> 16) & 0xFFFF)));
                    bool edge = f.WindowState == FormWindowState.Normal && (p.X < 6 || p.Y < 6 || p.X >= Width - 6 || p.Y >= Height - 6);
                    if (edge || InDragArea(p)) { m.Result = (IntPtr)(-1); return; }
                }
            }
            if (m.Msg == 0x10D && imeOn) ImeUpdate();     // a composition is starting
            base.WndProc(ref m);
        }

        void ShowFieldMenu(string id)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            string[,] items = { { "Cut", "cut" }, { "Copy", "copy" }, { "Paste", "paste" }, { "Select all", "selectall" } };
            for (int i = 0; i < 4; i++)
            {
                string cmd = items[i, 1];
                menu.Items.Add(Lang.T(items[i, 0]), null, delegate { ui.Command(id, cmd); Redraw(); });
            }
            Menus.Style(menu);
            menu.Closed += delegate { menu.Dispose(); };
            menu.Show(Cursor.Position);
        }

        PointF Pt(MouseEventArgs e) { return new PointF(e.X / scale, e.Y / scale); }

        protected override void OnMouseMove(MouseEventArgs e) { ui.Mouse = Pt(e); Redraw(); base.OnMouseMove(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            ui.Mouse = Pt(e);
            if (e.Button == MouseButtons.Left) { ui.Down = true; ui.Pressed = true; }
            if (e.Button == MouseButtons.Right) ui.RightPressed = true;
            Redraw(); base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            ui.Mouse = Pt(e);
            if (e.Button == MouseButtons.Left) { ui.Down = false; ui.Released = true; }
            Redraw(); base.OnMouseUp(e);
        }
        protected override void OnMouseLeave(EventArgs e) { ui.Mouse = new PointF(-9999, -9999); Redraw(); base.OnMouseLeave(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { ui.Wheel += e.Delta / scale; Redraw(); base.OnMouseWheel(e); }

        protected override bool IsInputKey(Keys keyData) { return true; }
        protected override void OnKeyPress(KeyPressEventArgs e) { ui.Typed += e.KeyChar; Redraw(); base.OnKeyPress(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (app.TourActive && app.TourKey(e.KeyCode)) { Redraw(); e.Handled = true; return; }
            ui.KeyHits.Add(e.KeyCode);
            if (e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9) app.Navigate((int)e.KeyCode - (int)Keys.D1);
            if (e.KeyCode == Keys.Escape && app.CloseTopWindow()) { Redraw(); e.Handled = true; return; }
            if (e.Control && !e.Shift && e.KeyCode == Keys.F) { app.Navigate("Accounts"); ui.Focus = "acc.search"; }
            if (e.Control && !e.Shift && e.KeyCode == Keys.L) app.LaunchSelectedNow();
            if (e.Control && e.Shift && e.KeyCode == Keys.S) Engine.StopAll();
            if (e.Control && e.Shift && e.KeyCode == Keys.K) { Store.Cfg.Compact = !Store.Cfg.Compact; Store.SaveSettings(); ApplyShell(); }
            if (e.Control && e.Shift && e.KeyCode == Keys.W) ShowMini();
            Redraw();
            base.OnKeyDown(e);
        }
    }
}
