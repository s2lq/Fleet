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
        Bitmap layer;
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
                int wallNeed = wallpaper.Animated ? Motion.WallMs : 0;
                if (front)
                {
                    if (uiNeed > 0 && now - lastUi >= uiNeed - 3) { Redraw(); lastUi = lastWall = now; }
                    else if (wallNeed > 0 && now - lastWall >= wallNeed - 3) { Invalidate(); lastWall = now; }
                    if (uiNeed > 0) next = uiNeed;
                    if (wallNeed > 0 && wallNeed < next) next = wallNeed;
                    if (next > 1000) next = 1000;
                }
                if (second) { Redraw(); lastUi = now; }
            }
            if (timer.Interval != next) timer.Interval = next;
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
                    ui.BeginFrame(lg, w, h);
                    try { app.Render(ui); }
                    catch (Exception ex) { Log.Add("Draw error: " + ex.Message); }
                    keepFast = ui.NeedFrames;
                    keepMs = ui.NeedMs;
                    Cursor = ui.WantHand ? Cursors.Hand : Cursors.Default;
                    ui.EndFrame();
                }
            }
            g.ResetTransform();
            g.ScaleTransform(scale, scale);
            wallpaper.Draw(g, w, h, ui.Time);
            g.ResetTransform();
            g.DrawImageUnscaled(layer, 0, 0);
        }

        PointF Pt(MouseEventArgs e) { return new PointF(e.X / scale, e.Y / scale); }

        protected override void OnMouseMove(MouseEventArgs e) { ui.Mouse = Pt(e); Redraw(); base.OnMouseMove(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            ui.Mouse = Pt(e);
            if (e.Button == MouseButtons.Left) { ui.Down = true; ui.Pressed = true; }
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
            ui.KeyHits.Add(e.KeyCode);
            if (e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9) app.Navigate((int)e.KeyCode - (int)Keys.D1);
            if (e.Control && e.Shift && e.KeyCode == Keys.K) { Store.Cfg.Compact = !Store.Cfg.Compact; Store.SaveSettings(); ApplyShell(); }
            if (e.Control && e.Shift && e.KeyCode == Keys.W) ShowMini();
            if (e.Control && e.KeyCode == Keys.V && Clipboard.ContainsText())
                ui.Typed += Clipboard.GetText().Replace("\r", "").Replace("\n", " ").Trim();
            Redraw();
            base.OnKeyDown(e);
        }
    }
}
