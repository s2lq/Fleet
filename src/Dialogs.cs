using System;
using System.Drawing;
using System.Windows.Forms;

namespace Fleet
{

    // a dialog with Fleet's own title bar. Controls are placed as if the form had no title; they are moved down under it.
    public class DialogForm : Form
    {
        const int HeaderH = 44;
        readonly Panel head;
        readonly string title;

        public static int Open;

        public DialogForm(string t, int w, int h)
        {
            title = t;
            Text = t;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            ClientSize = new Size(w, h + HeaderH);
            head = new Panel { Left = 1, Top = 1, Width = w - 2, Height = HeaderH - 1, BackColor = Pal.Panel, Tag = "hdr" };
            Label cap = new Label { Text = Lang.T(t), Left = 16, Top = 0, Height = HeaderH - 1, Width = w - 80, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Pal.Text, BackColor = Color.Transparent, Font = new Font(Fonts.Body.FontFamily, 10f, FontStyle.Bold) };
            Label x = new Label { Text = "\u2715", Left = w - 2 - 46, Top = 0, Width = 46, Height = HeaderH - 1, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Pal.Muted, BackColor = Color.Transparent, Cursor = Cursors.Hand, Font = new Font(Fonts.Body.FontFamily, 10f) };
            x.MouseEnter += delegate { x.BackColor = Color.FromArgb(196, 43, 28); x.ForeColor = Color.White; };
            x.MouseLeave += delegate { x.BackColor = Color.Transparent; x.ForeColor = Pal.Muted; };
            x.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            MouseEventHandler drag = delegate (object o, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left || Environment.OSVersion.Platform != PlatformID.Win32NT) return;
                Native.ReleaseCapture(); Native.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
            };
            head.MouseDown += drag; cap.MouseDown += drag;
            head.Controls.Add(cap); head.Controls.Add(x);
            Controls.Add(head);
            HandleCreated += delegate { Native.RoundCorners(Handle); };
            Shown += delegate { Open++; };
            FormClosed += delegate { Open--; };
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (!(e.Control.Tag is string && (string)e.Control.Tag == "hdr")) e.Control.Top += HeaderH;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Pal.Line2)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    public static class Dialogs
    {
        static Color Back { get { return Pal.Bg; } }
        static Color Field { get { return Pal.Panel2; } }

        static Form Make(string title, int w, int h)
        {
            DialogForm f = new DialogForm(title, w, h);
            f.BackColor = Back; f.ForeColor = Pal.Text;
            f.Font = new Font(Fonts.Body.FontFamily, 10f);
            return f;
        }

        static Label Lbl(string t, int x, int y, int w, Color c)
        {
            Label l = new Label { Text = t, Left = x, Top = y, Width = w, AutoSize = false, Height = 20, ForeColor = c, BackColor = Color.Transparent };
            return l;
        }

        static TextBox Box(int x, int y, int w, int h, string text, bool multi)
        {
            TextBox t = new TextBox { Left = x, Top = y, Width = w, Height = h, Text = text ?? "", Multiline = multi, BorderStyle = BorderStyle.FixedSingle, BackColor = Field, ForeColor = Pal.Text };
            if (multi) { t.ScrollBars = ScrollBars.Vertical; t.AcceptsReturn = true; }
            t.HandleCreated += delegate { Native.ModernControl(t.Handle); };
            return t;
        }

        static Button Btn(string text, int x, int y, int w, bool primary, DialogResult r)
        {
            Button b = new Button { Text = text, Left = x, Top = y, Width = w, Height = 34, FlatStyle = FlatStyle.Flat, DialogResult = r, Cursor = Cursors.Hand };
            b.FlatAppearance.BorderColor = primary ? Pal.A : Color.FromArgb(60, 62, 72);
            b.BackColor = primary ? Pal.A : Field;
            b.ForeColor = primary ? Color.FromArgb(6, 6, 10) : Pal.Text;
            return b;
        }

        public static string Paste(IWin32Window owner, string title, string label, string hint) { return Paste(owner, title, label, hint, ""); }

        public static string Paste(IWin32Window owner, string title, string label, string hint, string initial)
        {
            using (Form f = Make(title, 560, 330))
            {
                f.Controls.Add(Lbl(label, 20, 16, 520, Pal.Text));
                f.Controls.Add(Lbl(hint, 20, 38, 520, Pal.Muted));
                TextBox t = Box(20, 68, 520, 190, (initial ?? "").Replace("\n", "\r\n"), true);
                f.Controls.Add(t);
                Button ok = Btn("Add", 430, 276, 110, true, DialogResult.OK);
                Button no = Btn("Cancel", 310, 276, 110, false, DialogResult.Cancel);
                f.Controls.Add(ok); f.Controls.Add(no);
                f.AcceptButton = null; f.CancelButton = no;
                return f.ShowDialog(owner) == DialogResult.OK ? t.Text : null;
            }
        }

        public static bool EditAccount(IWin32Window owner, Account a)
        {
            using (Form f = Make("Edit " + a.Label, 540, 620))
            {
                string[] labels = { "Alias (nickname shown in Fleet)", "Note", "Group", "Tags (comma separated)", "Game for this account (link or place id)", "Private server link", "FPS cap for this account (0 = use the global one)", "Proxy (host:port or host:port:user:pass)", "Password (saved, for re-login)", "Roblox version for this account (blank = default)" };
                string[] values = { a.Alias, a.Note, a.Group, a.Tags, a.Place, a.PrivateLink, a.FpsCap.ToString(), a.Proxy, a.Password, a.RobloxVersion };
                TextBox[] boxes = new TextBox[labels.Length];
                int y = 14;
                for (int i = 0; i < labels.Length; i++)
                {
                    f.Controls.Add(Lbl(Lang.T(labels[i]), 20, y, 500, Pal.Muted));
                    boxes[i] = Box(20, y + 20, 500, 26, values[i], false);
                    if (i == 8) boxes[i].UseSystemPasswordChar = true;
                    f.Controls.Add(boxes[i]);
                    y += 56;
                }
                Button ok = Btn(Lang.T("Save"), 410, 572, 110, true, DialogResult.OK);
                Button no = Btn(Lang.T("Cancel"), 290, 572, 110, false, DialogResult.Cancel);
                f.Controls.Add(ok); f.Controls.Add(no);
                f.AcceptButton = ok; f.CancelButton = no;
                if (f.ShowDialog(owner) != DialogResult.OK) return false;
                a.Alias = boxes[0].Text.Trim(); a.Note = boxes[1].Text.Trim(); a.Group = boxes[2].Text.Trim(); a.Tags = boxes[3].Text.Trim();
                a.Place = boxes[4].Text.Trim(); a.PrivateLink = boxes[5].Text.Trim();
                int fps; a.FpsCap = int.TryParse(boxes[6].Text, out fps) ? Math.Max(0, Math.Min(1000, fps)) : 0;
                a.Proxy = boxes[7].Text.Trim(); a.ProxyAuto = false; a.Password = boxes[8].Text; a.RobloxVersion = boxes[9].Text.Trim();
                return true;
            }
        }

        public static string Prompt(IWin32Window owner, string title, string label, string initial)
        {
            using (Form f = Make(title, 480, 160))
            {
                f.Controls.Add(Lbl(label, 20, 16, 440, Pal.Muted));
                TextBox t = Box(20, 42, 440, 26, initial, false); f.Controls.Add(t);
                Button ok = Btn(Lang.T("OK"), 350, 100, 110, true, DialogResult.OK);
                Button no = Btn(Lang.T("Cancel"), 230, 100, 110, false, DialogResult.Cancel);
                f.Controls.Add(ok); f.Controls.Add(no);
                f.AcceptButton = ok; f.CancelButton = no;
                return f.ShowDialog(owner) == DialogResult.OK ? t.Text : null;
            }
        }

        // one password box, or two when you are choosing a new one
        public static string Password(IWin32Window owner, string title, string label, bool confirm)
        {
            using (Form f = Make(title, 460, confirm ? 230 : 170))
            {
                f.Controls.Add(Lbl(label, 20, 16, 420, Pal.Muted));
                TextBox t1 = Box(20, 42, 420, 26, "", false); t1.UseSystemPasswordChar = true; f.Controls.Add(t1);
                TextBox t2 = null;
                if (confirm)
                {
                    f.Controls.Add(Lbl(Lang.T("Type it again"), 20, 80, 420, Pal.Muted));
                    t2 = Box(20, 106, 420, 26, "", false); t2.UseSystemPasswordChar = true; f.Controls.Add(t2);
                }
                Button ok = Btn(Lang.T("OK"), 330, confirm ? 170 : 110, 110, true, DialogResult.None);
                Button no = Btn(Lang.T("Cancel"), 210, confirm ? 170 : 110, 110, false, DialogResult.Cancel);
                string result = null;
                ok.Click += delegate
                {
                    if (t1.Text.Length == 0) { MessageBox.Show(f, Lang.T("The password cannot be empty.")); return; }
                    if (confirm && t1.Text != t2.Text) { MessageBox.Show(f, Lang.T("The two passwords do not match.")); return; }
                    result = t1.Text; f.DialogResult = DialogResult.OK;
                };
                f.Controls.Add(ok); f.Controls.Add(no);
                f.AcceptButton = ok; f.CancelButton = no;
                return f.ShowDialog(owner) == DialogResult.OK ? result : null;
            }
        }

        public static bool ThemeEditor(IWin32Window owner, CustomTheme t)
        {
            using (Form f = Make(Lang.T("Edit colours"), 420, 420))
            {
                string[] labels = { "Name", "Background (#RRGGBB)", "Panels", "Borders", "Text", "Muted text" };
                string[] vals = { t.Name, t.Bg, t.Panel, t.Line, t.Text, t.Muted };
                TextBox[] b = new TextBox[6];
                int y = 14;
                for (int i = 0; i < 6; i++) { f.Controls.Add(Lbl(Lang.T(labels[i]), 20, y, 380, Pal.Muted)); b[i] = Box(20, y + 20, 380, 26, vals[i], false); f.Controls.Add(b[i]); y += 56; }
                Button ok = Btn(Lang.T("Save"), 290, 372, 110, true, DialogResult.None);
                Button no = Btn(Lang.T("Cancel"), 170, 372, 110, false, DialogResult.Cancel);
                ok.Click += delegate
                {
                    for (int i = 1; i < 6; i++) if (Themes.Hex(b[i].Text).IsEmpty) { MessageBox.Show(f, Lang.T("Use colours like #1A2B3C.")); return; }
                    if (b[0].Text.Trim().Length == 0) { MessageBox.Show(f, Lang.T("Give the theme a name.")); return; }
                    f.DialogResult = DialogResult.OK;
                };
                f.Controls.Add(ok); f.Controls.Add(no);
                if (f.ShowDialog(owner) != DialogResult.OK) return false;
                t.Name = b[0].Text.Trim(); t.Bg = b[1].Text.Trim(); t.Panel = b[2].Text.Trim(); t.Line = b[3].Text.Trim(); t.Text = b[4].Text.Trim(); t.Muted = b[5].Text.Trim();
                return true;
            }
        }

        public static string AddServer(IWin32Window owner, out string name, out string link)
        {
            name = null; link = null;
            using (Form f = Make(Lang.T("Add a private server"), 520, 230))
            {
                f.Controls.Add(Lbl(Lang.T("Name"), 20, 14, 480, Pal.Muted));
                TextBox n = Box(20, 36, 480, 26, "", false); f.Controls.Add(n);
                f.Controls.Add(Lbl(Lang.T("Private server link"), 20, 76, 480, Pal.Muted));
                TextBox l = Box(20, 98, 480, 26, "", false); f.Controls.Add(l);
                Button ok = Btn(Lang.T("Add"), 390, 170, 110, true, DialogResult.OK);
                Button no = Btn(Lang.T("Cancel"), 270, 170, 110, false, DialogResult.Cancel);
                f.Controls.Add(ok); f.Controls.Add(no);
                f.AcceptButton = ok; f.CancelButton = no;
                if (f.ShowDialog(owner) != DialogResult.OK) return null;
                name = n.Text.Trim(); link = l.Text.Trim();
                return "ok";
            }
        }

        public static bool Confirm(IWin32Window owner, string title, string text, string yes)
        {
            using (Form f = Make(title, 440, 170))
            {
                Label l = Lbl(text, 20, 20, 400, Pal.Text); l.Height = 80; f.Controls.Add(l);
                Button ok = Btn(yes, 300, 120, 120, true, DialogResult.OK);
                Button no = Btn("Cancel", 180, 120, 110, false, DialogResult.Cancel);
                f.Controls.Add(ok); f.Controls.Add(no);
                f.CancelButton = no;
                return f.ShowDialog(owner) == DialogResult.OK;
            }
        }
    }
}
