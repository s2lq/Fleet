using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace Fleet
{
    // single-line text editing for the hand-drawn interface: caret, selection, clipboard, undo, mouse, IME position
    public partial class Ui
    {
        class FieldState
        {
            public int Caret, Anchor;
            public float Scroll;
            public string Cmd;
            public float LastClickAt = -9f;
            public PointF LastClickPos;
            public readonly List<string> Undo = new List<string>();
            public readonly List<int> UndoCaret = new List<int>();
        }

        readonly Dictionary<string, FieldState> fields = new Dictionary<string, FieldState>();
        public bool RightPressed;
        public string ContextField;
        public bool ImeValid;
        public PointF ImeAt;
        // replaceable so the editing logic can be tested without a real clipboard
        public static Func<string> ClipGet = delegate { try { return Clipboard.ContainsText() ? Clipboard.GetText() : ""; } catch (Exception) { return ""; } };
        public static Action<string> ClipSet = delegate(string t) { try { if (!string.IsNullOrEmpty(t)) Clipboard.SetText(t); } catch (Exception) { } };
        public static Func<Keys> ModKeys = delegate { return Control.ModifierKeys; };

        FieldState State(string id)
        {
            FieldState s;
            if (!fields.TryGetValue(id, out s)) { s = new FieldState(); fields[id] = s; }
            return s;
        }

        // asked for by the right-click menu
        public void Command(string id, string cmd) { State(id).Cmd = cmd; }
        public int CaretOf(string id) { return State(id).Caret; }
        public string SelectionOf(string id, string text) { FieldState s = State(id); int a = Math.Min(s.Caret, s.Anchor), b = Math.Max(s.Caret, s.Anchor); return a < b && b <= text.Length ? text.Substring(a, b - a) : ""; }

        // raw text (never translated)
        float Wide(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            return (float)Math.Ceiling(G.MeasureString(s, Fonts.Body, 100000, sf).Width);
        }

        static bool IsLow(char c) { return c >= '\uDC00' && c <= '\uDFFF'; }
        static bool IsHigh(char c) { return c >= '\uD800' && c <= '\uDBFF'; }
        static int Prev(string s, int i) { if (i <= 0) return 0; i--; if (i > 0 && IsLow(s[i]) && IsHigh(s[i - 1])) i--; return i; }
        static int Next(string s, int i) { if (i >= s.Length) return s.Length; i++; if (i < s.Length && IsLow(s[i]) && IsHigh(s[i - 1])) i++; return i; }

        static bool WordChar(char c) { return char.IsLetterOrDigit(c) || c == '_'; }
        static int WordLeft(string s, int i)
        {
            while (i > 0 && !WordChar(s[i - 1])) i = Prev(s, i);
            while (i > 0 && WordChar(s[i - 1])) i = Prev(s, i);
            return i;
        }
        static int WordRight(string s, int i)
        {
            while (i < s.Length && !WordChar(s[i])) i = Next(s, i);
            while (i < s.Length && WordChar(s[i])) i = Next(s, i);
            return i;
        }

        int IndexAt(string s, float x)
        {
            if (x <= 0) return 0;
            int best = 0; float bestD = x;
            for (int i = Next(s, 0); ; i = Next(s, i))
            {
                float d = Math.Abs(Wide(s.Substring(0, i)) - x);
                if (d < bestD) { bestD = d; best = i; }
                if (i >= s.Length) break;
            }
            return best;
        }

        public string TextField(string id, RectangleF r, string value, string hint)
        {
            FieldState st = State(id);
            bool focus = Focus == id;
            bool over = Over(r);
            if (over) WantHand = true;
            Anim(id + ".f", focus);
            Fill(r, 9, Pal.Panel);
            Stroke(r, 9, focus ? Pal.Alpha(Pal.A, 200) : Pal.Line, 1f);
            string v = value ?? "";
            if (st.Caret > v.Length) st.Caret = v.Length;
            if (st.Anchor > v.Length) st.Anchor = v.Length;
            RectangleF inner = new RectangleF(r.X + 14, r.Y, r.Width - 28, r.Height);

            // ---- mouse: click to place the caret, drag to select, double click for a word
            if ((Pressed || RightPressed) && over)
            {
                if (!focus) { Focus = id; focus = true; }
                float mx = Mouse.X;
                if (id.Length > 0) { /* inside a scrolled page the field is drawn shifted, local carries that */ }
                int idx = IndexAt(v, local.X - inner.X + st.Scroll);
                bool shift = (ModKeys() & Keys.Shift) != 0;
                if (RightPressed)
                {
                    int a0 = Math.Min(st.Caret, st.Anchor), b0 = Math.Max(st.Caret, st.Anchor);
                    if (idx < a0 || idx > b0) { st.Caret = st.Anchor = idx; }
                    ContextField = id;
                }
                else
                {
                    bool dbl = Time - st.LastClickAt < 0.4f && Math.Abs(local.X - st.LastClickPos.X) < 5;
                    if (dbl) { st.Anchor = WordLeft(v, Math.Min(v.Length, idx + 1)); st.Caret = WordRight(v, st.Anchor); st.LastClickAt = -9f; }
                    else { st.Caret = idx; if (!shift) st.Anchor = idx; st.LastClickAt = Time; st.LastClickPos = local; }
                    Active = id + ".drag";
                }
            }
            else if (Pressed && focus && !over) { Focus = null; focus = false; }
            if (Active == id + ".drag")
            {
                if (Down) st.Caret = IndexAt(v, local.X - inner.X + st.Scroll);
                if (Released) Active = null;
            }

            // ---- keyboard
            if (focus)
            {
                Keys mods = ModKeys();
                bool ctrl = (mods & Keys.Control) != 0, shift = (mods & Keys.Shift) != 0;
                foreach (Keys k in KeyHits)
                {
                    int c = st.Caret;
                    switch (k)
                    {
                        case Keys.Left: c = ctrl ? WordLeft(v, c) : (!shift && st.Caret != st.Anchor ? Math.Min(st.Caret, st.Anchor) : Prev(v, c)); Move(st, c, shift, !shift && st.Caret != st.Anchor); break;
                        case Keys.Right: c = ctrl ? WordRight(v, c) : (!shift && st.Caret != st.Anchor ? Math.Max(st.Caret, st.Anchor) : Next(v, c)); Move(st, c, shift, !shift && st.Caret != st.Anchor); break;
                        case Keys.Home: Move(st, 0, shift, false); break;
                        case Keys.End: Move(st, v.Length, shift, false); break;
                        case Keys.Delete:
                            Remember(st, v);
                            if (st.Caret != st.Anchor) v = DeleteSel(st, v);
                            else if (st.Caret < v.Length) v = v.Remove(st.Caret, Next(v, st.Caret) - st.Caret);
                            break;
                        case Keys.A: if (ctrl) { st.Anchor = 0; st.Caret = v.Length; } break;
                        case Keys.C: if (ctrl) ClipSet(SelectionOf(id, v)); break;
                        case Keys.X: if (ctrl && st.Caret != st.Anchor) { ClipSet(SelectionOf(id, v)); Remember(st, v); v = DeleteSel(st, v); } break;
                        case Keys.V: if (ctrl) { Remember(st, v); v = Insert(st, v, ClipGet()); } break;
                        case Keys.Z: if (ctrl && st.Undo.Count > 0) { v = st.Undo[st.Undo.Count - 1]; st.Caret = st.Anchor = Math.Min(v.Length, st.UndoCaret[st.UndoCaret.Count - 1]); st.Undo.RemoveAt(st.Undo.Count - 1); st.UndoCaret.RemoveAt(st.UndoCaret.Count - 1); } break;
                    }
                }
                if (st.Cmd != null)
                {
                    string cmd = st.Cmd; st.Cmd = null;
                    if (cmd == "selectall") { st.Anchor = 0; st.Caret = v.Length; }
                    else if (cmd == "copy") ClipSet(SelectionOf(id, v));
                    else if (cmd == "cut" && st.Caret != st.Anchor) { ClipSet(SelectionOf(id, v)); Remember(st, v); v = DeleteSel(st, v); }
                    else if (cmd == "paste") { Remember(st, v); v = Insert(st, v, ClipGet()); }
                }
                if (Typed.Length > 0) Remember(st, v);
                foreach (char ch in Typed)
                {
                    if (ch == '\b') { if (st.Caret != st.Anchor) v = DeleteSel(st, v); else if (st.Caret > 0) { int p = Prev(v, st.Caret); v = v.Remove(p, st.Caret - p); st.Caret = st.Anchor = p; } }
                    else if (ch >= ' ' && ch != 127) v = Insert(st, v, ch.ToString());
                }
                if (KeyHits.Contains(Keys.Enter) || KeyHits.Contains(Keys.Escape)) { Focus = null; focus = false; }
                Want(250);
            }
            if (st.Caret > v.Length) st.Caret = v.Length;
            if (st.Anchor > v.Length) st.Anchor = v.Length;

            // ---- drawing
            if (v.Length == 0 && !focus) { RawText(hint, inner, Pal.Faint, 0); return v; }
            float caretX = Wide(v.Substring(0, st.Caret));
            if (caretX - st.Scroll > inner.Width - 6) st.Scroll = caretX - inner.Width + 6;
            if (caretX - st.Scroll < 0) st.Scroll = caretX;
            float total = Wide(v);
            if (total - st.Scroll < inner.Width - 6) st.Scroll = Math.Max(0, total - inner.Width + 6);
            GraphicsState gs = G.Save();
            G.SetClip(new RectangleF(inner.X - 2, r.Y + 2, inner.Width + 4, r.Height - 4), CombineMode.Intersect);
            if (focus && st.Caret != st.Anchor)
            {
                int a = Math.Min(st.Caret, st.Anchor), b = Math.Max(st.Caret, st.Anchor);
                float x0 = inner.X - st.Scroll + Wide(v.Substring(0, a)), x1 = inner.X - st.Scroll + Wide(v.Substring(0, b));
                Fill(new RectangleF(x0, r.Y + 8, Math.Max(2, x1 - x0), r.Height - 16), 3, Pal.Alpha(Pal.A, 90));
            }
            RawText(v, new RectangleF(inner.X - st.Scroll, inner.Y, inner.Width + st.Scroll + 400, inner.Height), Pal.Text, 0);
            if (focus && ((int)(Time * 2)) % 2 == 0)
                using (Pen p = new Pen(Pal.Text, 1.4f)) G.DrawLine(p, inner.X - st.Scroll + caretX, r.Y + 9, inner.X - st.Scroll + caretX, r.Bottom - 9);
            G.Restore(gs);
            if (focus)
            {
                PointF[] pt = { new PointF(inner.X - st.Scroll + caretX, r.Bottom - 6) };
                using (Matrix m = G.Transform) m.TransformPoints(pt);
                ImeAt = pt[0]; ImeValid = true;
            }
            return v;
        }

        void RawText(string s, RectangleF r, Color c, int unused)
        {
            if (string.IsNullOrEmpty(s)) return;
            StringFormat fmt = new StringFormat(sf);
            fmt.Alignment = StringAlignment.Near; fmt.LineAlignment = StringAlignment.Center;
            fmt.Trimming = StringTrimming.None;
            using (SolidBrush b = new SolidBrush(c)) G.DrawString(s, Fonts.Body, b, r, fmt);
            fmt.Dispose();
        }

        static void Move(FieldState st, int to, bool extend, bool collapsed) { st.Caret = to; if (!extend) st.Anchor = to; }

        static void Remember(FieldState st, string v)
        {
            if (st.Undo.Count > 0 && st.Undo[st.Undo.Count - 1] == v) return;
            st.Undo.Add(v); st.UndoCaret.Add(st.Caret);
            if (st.Undo.Count > 30) { st.Undo.RemoveAt(0); st.UndoCaret.RemoveAt(0); }
        }

        static string DeleteSel(FieldState st, string v)
        {
            int a = Math.Min(st.Caret, st.Anchor), b = Math.Max(st.Caret, st.Anchor);
            st.Caret = st.Anchor = a;
            return b > a ? v.Remove(a, b - a) : v;
        }

        static string Insert(FieldState st, string v, string text)
        {
            if (string.IsNullOrEmpty(text)) return v;
            text = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
            if (st.Caret != st.Anchor) v = DeleteSel(st, v);
            if (v.Length + text.Length > 4000) return v;
            v = v.Insert(st.Caret, text);
            st.Caret = st.Anchor = st.Caret + text.Length;
            return v;
        }
    }
}
