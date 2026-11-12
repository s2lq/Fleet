using System; using System.Drawing; using System.Windows.Forms; using Fleet;
class TextFieldTests {
 static int fails = 0;
 static void Chk(bool c, string n) { Console.WriteLine((c ? "PASS " : "FAIL ") + n); if (!c) fails++; }
 static Bitmap bmp = new Bitmap(900, 300); static Graphics g = Graphics.FromImage(bmp); static Ui ui = new Ui();
 static string txt = ""; static RectangleF rect = new RectangleF(20, 20, 400, 42);
 static Keys mods = Keys.None; static string clip = "";
 static void Frame() { ui.BeginFrame(g, 900, 300); txt = ui.TextField("f", rect, txt, "hint"); ui.EndFrame(); }
 static void Click(float x) { ui.Mouse = new PointF(x, 40); ui.Pressed = true; ui.Down = true; Frame(); ui.Down = false; ui.Released = true; Frame(); }
 static void Type(string s) { ui.Typed = s; Frame(); }
 static void Key(Keys k, Keys m = Keys.None) { mods = m; ui.KeyHits.Add(k); Frame(); mods = Keys.None; }
 static void Main() {
  Ui.ModKeys = delegate { return mods; }; Ui.ClipGet = delegate { return clip; }; Ui.ClipSet = delegate(string t) { clip = t; };
  Store.Cfg = new Settings(); Frame();
  Click(40); Type("hello world");
  Chk(txt == "hello world" && ui.CaretOf("f") == 11, "typing");
  for (int i = 0; i < 5; i++) Key(Keys.Left);
  Chk(ui.CaretOf("f") == 6, "left x5 -> caret 6");
  Type("X"); Chk(txt == "hello Xworld", "insert at caret");
  Key(Keys.Home); Key(Keys.End, Keys.Shift); Chk(ui.SelectionOf("f", txt) == txt, "home then shift+end selects all");
  Type("Z"); Chk(txt == "Z", "typing replaces the selection");
  Type("abc def"); Chk(txt == "Zabc def", "more typing");
  Key(Keys.Left, Keys.Control); Chk(ui.CaretOf("f") == 5, "ctrl+left jumps a word, caret=" + ui.CaretOf("f"));
  Key(Keys.Right, Keys.Shift); Key(Keys.Right, Keys.Shift); Chk(ui.SelectionOf("f", txt) == "de", "shift+right x2 selects 'de'");
  Key(Keys.X, Keys.Control); Chk(clip == "de" && txt == "Zabc f", "ctrl+x cuts (" + txt + ")");
  Key(Keys.V, Keys.Control); Chk(txt == "Zabc def", "ctrl+v pastes");
  Key(Keys.A, Keys.Control); Chk(ui.SelectionOf("f", txt) == txt, "ctrl+a");
  Key(Keys.C, Keys.Control); Chk(clip == "Zabc def", "ctrl+c copies");
  Key(Keys.End); Type("\b"); Chk(txt == "Zabc de", "backspace");
  Key(Keys.Left); Key(Keys.Delete); Chk(txt == "Zabc d", "delete key (" + txt + ")");
  Key(Keys.Z, Keys.Control); Chk(txt != "Zabc d", "undo changes the text (" + txt + ")");
  txt = ""; Key(Keys.A, Keys.Control); Type("日本語");
  Chk(txt == "日本語", "non-Latin text");
  Type("😀"); Chk(txt.Length == 5, "emoji (surrogate pair) typed");
  Type("\b"); Chk(txt == "日本語", "backspace removes the whole emoji");
  Type("😀"); Key(Keys.Left); Chk(ui.CaretOf("f") == 3, "caret skips an emoji as one character");
  Key(Keys.End);
  Click(30); Chk(ui.CaretOf("f") == 0, "click at the far left puts the caret first");
  Click(380); Chk(ui.CaretOf("f") == txt.Length, "click at the far right puts the caret last");
  txt = "one two three"; Click(380); Key(Keys.Home);
  ui.Mouse = new PointF(40, 40); ui.Pressed = true; ui.Down = true; Frame(); ui.Down = false; ui.Released = true; Frame();
  ui.Pressed = true; ui.Down = true; Frame(); ui.Down = false; ui.Released = true; Frame();
  Chk(ui.SelectionOf("f", txt).Trim().Length > 0 && ui.SelectionOf("f", txt).Contains("one"), "double click selects a word ('" + ui.SelectionOf("f", txt) + "')");
  ui.Mouse = new PointF(40, 40); ui.RightPressed = true; Frame(); Chk(ui.ContextField == "f", "right click asks for the menu");
  ui.Command("f", "selectall"); Frame(); Chk(ui.SelectionOf("f", txt) == txt, "menu command select all");
  txt = ""; Click(200); string longs = new string('w', 80); Type(longs); Chk(txt.Length == 80 && ui.CaretOf("f") == 80, "long text scrolls without breaking");
  Chk(ui.ImeValid, "caret position reported for the input method");
  Environment.Exit(fails);
 }
}
