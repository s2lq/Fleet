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
        int ticks;
        bool keepAnimating;

        public Canvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = Pal.Bg;
            app = new App(this);
            // ~30fps
            timer.Interval = 33;
            timer.Tick += delegate
            {
                ticks++;
                // roughly once a second
                if (ticks % 30 == 0) { Engine.Tick(); app.EverySecond(); }
                if (keepAnimating || wallpaper.Animated) Invalidate();
            };
            timer.Start();
            Log.Changed += delegate { try { if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { Invalidate(); }); } catch (Exception) { } };
        }

        public App AppRef { get { return app; } }
        public void StartUp() { app.StartUp(); }
        public void RunPreset(Preset p) { app.RunPreset(p); }
        public void ApplyShell() { MainForm f = FindForm() as MainForm; if (f != null) f.ApplyShell(); }
        public void ShowMini() { MainForm f = FindForm() as MainForm; if (f != null) f.ToggleMini(); }

        public void Navigate(string page) { app.Navigate(page); Invalidate(); }

        // dialogs must not open in the middle of a paint
        public void Post(Action a) { BeginInvoke((MethodInvoker)delegate { try { a(); } catch (Exception ex) { Log.Add("Error: " + ex.Message); } Invalidate(); }); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            scale = Math.Max(1f, g.DpiX / 96f);
            g.ResetTransform();
            g.ScaleTransform(scale, scale);
            int w = (int)Math.Ceiling(Width / scale), h = (int)Math.Ceiling(Height / scale);
            ui.Time = (float)(DateTime.UtcNow - start).TotalSeconds;
            wallpaper.Draw(g, w, h, ui.Time);
            ui.BeginFrame(g, w, h);
            try { app.Render(ui); }
            catch (Exception ex) { Log.Add("Draw error: " + ex.Message); }
            keepAnimating = ui.NeedFrames;
            Cursor = ui.WantHand ? Cursors.Hand : Cursors.Default;
            ui.EndFrame();
        }

        PointF Pt(MouseEventArgs e) { return new PointF(e.X / scale, e.Y / scale); }

        protected override void OnMouseMove(MouseEventArgs e) { ui.Mouse = Pt(e); Invalidate(); base.OnMouseMove(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            ui.Mouse = Pt(e);
            if (e.Button == MouseButtons.Left) { ui.Down = true; ui.Pressed = true; }
            Invalidate(); base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            ui.Mouse = Pt(e);
            if (e.Button == MouseButtons.Left) { ui.Down = false; ui.Released = true; }
            Invalidate(); base.OnMouseUp(e);
        }
        protected override void OnMouseLeave(EventArgs e) { ui.Mouse = new PointF(-9999, -9999); Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { ui.Wheel += e.Delta / scale; Invalidate(); base.OnMouseWheel(e); }

        protected override bool IsInputKey(Keys keyData) { return true; }
        protected override void OnKeyPress(KeyPressEventArgs e) { ui.Typed += e.KeyChar; Invalidate(); base.OnKeyPress(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            ui.KeyHits.Add(e.KeyCode);
            if (e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9) app.Navigate((int)e.KeyCode - (int)Keys.D1);
            if (e.Control && e.Shift && e.KeyCode == Keys.K) { Store.Cfg.Compact = !Store.Cfg.Compact; Store.SaveSettings(); ApplyShell(); }
            if (e.Control && e.Shift && e.KeyCode == Keys.W) ShowMini();
            if (e.Control && e.KeyCode == Keys.V && Clipboard.ContainsText())
                ui.Typed += Clipboard.GetText().Replace("\r", "").Replace("\n", " ").Trim();
            Invalidate();
            base.OnKeyDown(e);
        }
    }
}
