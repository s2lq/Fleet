using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fleet
{
    public class MainForm : Form
    {
        public readonly Canvas View;
        NotifyIcon tray;
        ContextMenuStrip menu;
        MiniForm mini;
        bool quitting;
        readonly bool startHidden;

        public MainForm(bool minimized)
        {
            startHidden = minimized;
            Text = "Fleet";
            BackColor = Pal.Bg;
            StartPosition = FormStartPosition.CenterScreen;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(Math.Min(1320, wa.Width - 60), Math.Min(840, wa.Height - 80));
            MinimumSize = new Size(Math.Min(1000, wa.Width), Math.Min(640, wa.Height));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            HandleCreated += delegate { Native.DarkTitleBar(Handle); };
            View = new Canvas { Dock = DockStyle.Fill };
            Controls.Add(View);

            menu = new ContextMenuStrip();
            menu.Items.Add(Lang.T("Show Fleet"), null, delegate { ShowMain(); });
            menu.Items.Add(Lang.T("Mini window"), null, delegate { ToggleMini(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Lang.T("Launch all"), null, delegate { LaunchAll(); });
            menu.Items.Add(Lang.T("Stop all"), null, delegate { Engine.StopAll(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Lang.T("Quit"), null, delegate { quitting = true; Close(); });
            menu.BackColor = Pal.Panel; menu.ForeColor = Pal.Text;
            tray = new NotifyIcon { Text = "Fleet", ContextMenuStrip = menu };
            try { tray.Icon = Icon ?? SystemIcons.Application; } catch (Exception) { tray.Icon = SystemIcons.Application; }
            tray.DoubleClick += delegate { ShowMain(); };
            Shell.Tray = tray;
            Resize += delegate { if (WindowState == FormWindowState.Minimized && Store.Cfg.MinToTray && Store.Cfg.Tray) Hide(); };
        }

        protected override void SetVisibleCore(bool value)
        {
            if (startHidden && !IsHandleCreated) { CreateHandle(); base.SetVisibleCore(false); return; }
            base.SetVisibleCore(value);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyShell();
            View.StartUp();
            if (!string.IsNullOrEmpty(Store.Cfg.StartupPreset))
            {
                Preset p = Store.Presets.FirstOrDefault(x => x.Name == Store.Cfg.StartupPreset);
                if (p != null) View.RunPreset(p);
            }
        }

        public void ApplyShell()
        {
            tray.Visible = Store.Cfg.Tray;
            MinimumSize = Store.Cfg.Compact ? new Size(820, 520) : new Size(Math.Min(1000, Screen.PrimaryScreen.WorkingArea.Width), Math.Min(640, Screen.PrimaryScreen.WorkingArea.Height));
            Unhot();
            if (Store.Cfg.Hotkeys && Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                uint mod = 0x1 | 0x2;
                Native.RegisterHotKey(Handle, 1, mod, 0x74);
                Native.RegisterHotKey(Handle, 2, mod, 0x75);
                Native.RegisterHotKey(Handle, 3, mod, 0x76);
                Native.RegisterHotKey(Handle, 4, mod, 0x77);
                Native.RegisterHotKey(Handle, 5, mod, 0x78);
            }
            View.Invalidate();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x312)
            {
                switch ((int)m.WParam)
                {
                    case 1: LaunchAll(); break;
                    case 2: Engine.StopAll(); break;
                    case 3: if (Visible && WindowState != FormWindowState.Minimized) Hide(); else ShowMain(); break;
                    case 4: if (Macro.Recording) Macro.StopRecord(); else Macro.Record(); break;
                    case 5: if (Macro.Playing) Macro.StopPlay(); else Macro.Play(Store.Cfg.MacroLoops); break;
                }
            }
            base.WndProc(ref m);
        }

        void Unhot()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            for (int i = 1; i <= 5; i++) Native.UnregisterHotKey(Handle, i);
        }

        void LaunchAll()
        {
            if (Store.Accounts.Count > 0) View.AppRef.LaunchList(Store.Accounts.OrderBy(a => a.Order).ToList());
        }

        public void ShowMain()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        public void ToggleMini()
        {
            if (mini != null && !mini.IsDisposed) { mini.Close(); return; }
            mini = new MiniForm(View.AppRef);
            mini.FormClosed += delegate { mini = null; if (!quitting) ShowMain(); };
            Hide();
            mini.Show();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!quitting && e.CloseReason == CloseReason.UserClosing && Store.Cfg.CloseToTray && Store.Cfg.Tray) { e.Cancel = true; Hide(); return; }
            if (mini != null && !mini.IsDisposed) { quitting = true; mini.Close(); }
            Unhot();
            tray.Visible = false;
            base.OnFormClosing(e);
        }
    }

    // a small always-on-top launcher: one row per account
    public class MiniForm : Form
    {
        readonly App app;
        readonly MiniCanvas view;

        public MiniForm(App a)
        {
            app = a;
            Text = "Fleet";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            TopMost = true;
            BackColor = Pal.Bg;
            ClientSize = new Size(300, 400);
            StartPosition = FormStartPosition.Manual;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Point at = new Point(Store.Cfg.MiniX, Store.Cfg.MiniY);
            if (at.X < 0 || at.Y < 0 || at.X > wa.Right - 100 || at.Y > wa.Bottom - 100) at = new Point(wa.Right - 330, wa.Top + 60);
            Location = at;
            view = new MiniCanvas(a, this) { Dock = DockStyle.Fill };
            Controls.Add(view);
            HandleCreated += delegate { Native.DarkTitleBar(Handle); };
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Store.Cfg.MiniX = Left; Store.Cfg.MiniY = Top; Store.SaveSettings();
            base.OnFormClosed(e);
        }
    }

    public class MiniCanvas : Control
    {
        readonly Ui ui = new Ui();
        readonly App app;
        readonly Form host;
        readonly Timer timer = new Timer();
        readonly ScrollState scroll = new ScrollState();
        readonly DateTime start = DateTime.UtcNow;
        float scale = 1f;

        public MiniCanvas(App a, Form h)
        {
            app = a; host = h;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            timer.Interval = 250;
            timer.Tick += delegate { Invalidate(); };
            timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            scale = Math.Max(1f, g.DpiX / 96f);
            g.ResetTransform(); g.ScaleTransform(scale, scale);
            float w = Width / scale, h = Height / scale;
            g.Clear(Pal.Bg);
            ui.Time = (float)(DateTime.UtcNow - start).TotalSeconds;
            ui.BeginFrame(g, w, h);
            List<Account> list = Store.Accounts.OrderBy(a => a.Order).ToList();
            RectangleF view = new RectangleF(0, 0, w, h - 112);
            ui.BeginScroll(view, list.Count * 46 + 12, scroll);
            float y = 8;
            foreach (Account a in list)
            {
                Running r = Engine.Of(a);
                ui.Fill(new RectangleF(16, y + 15, 10, 10), 5, r != null ? Pal.Good : Pal.Faint);
                ui.Text(Ui.Fit(ui, a.Label, Fonts.Body, 150), 36, y + 10, Fonts.Body, Pal.Text);
                if (r == null) { if (ui.Button("m.l" + a.Id, new RectangleF(w - 100, y + 3, 84, 34), "Launch", Btn.Ghost, null)) app.LaunchList(new List<Account> { a }); }
                else if (ui.Button("m.s" + a.Id, new RectangleF(w - 100, y + 3, 84, 34), "Stop", Btn.Danger, null)) Engine.Kill(r);
                y += 46;
            }
            ui.EndScroll();
            if (ui.Button("m.all", new RectangleF(12, h - 104, 132, 40), "Launch all", Btn.Primary, null) && list.Count > 0) app.LaunchList(list);
            if (ui.Button("m.stop", new RectangleF(w - 144, h - 104, 132, 40), "Stop all", Btn.Danger, null)) Engine.StopAll();
            if (ui.Button("m.open", new RectangleF(12, h - 56, w - 24, 40), "Open Fleet", Btn.Ghost, null)) BeginInvoke((MethodInvoker)delegate { host.Close(); });
            ui.Overlay();
            ui.EndFrame();
        }

        PointF Pt(MouseEventArgs e) { return new PointF(e.X / scale, e.Y / scale); }
        protected override void OnMouseMove(MouseEventArgs e) { ui.Mouse = Pt(e); Invalidate(); base.OnMouseMove(e); }
        protected override void OnMouseDown(MouseEventArgs e) { ui.Mouse = Pt(e); if (e.Button == MouseButtons.Left) { ui.Down = true; ui.Pressed = true; } Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { ui.Mouse = Pt(e); if (e.Button == MouseButtons.Left) { ui.Down = false; ui.Released = true; } Invalidate(); base.OnMouseUp(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { ui.Wheel += e.Delta / scale; Invalidate(); base.OnMouseWheel(e); }
    }
}
