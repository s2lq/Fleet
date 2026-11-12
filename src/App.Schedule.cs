using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Fleet
{
    // timed jobs: launch a preset, launch everyone, or stop everything
    public partial class App
    {
        string schName = "", schTime = "20:00";
        int schDays = 127, schAction;
        int schPreset = -1;
        static readonly string[] DayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        static readonly string[] SchActions = { "Launch preset", "Launch all", "Stop all" };

        // called every few seconds
        void RunSchedule()
        {
            DateTime now = DateTime.Now;
            string stamp = now.ToString("yyyyMMddHHmm"), hm = now.ToString("HH:mm");
            int bit = ((int)now.DayOfWeek + 6) % 7;
            foreach (ScheduleItem it in Store.Cfg.Schedule.ToList())
            {
                if (!it.Enabled || it.Time != hm || it.LastRun == stamp || ((it.Days >> bit) & 1) == 0) continue;
                it.LastRun = stamp;
                Store.SaveSettings();
                Log.Add("Schedule: " + (it.Name.Length > 0 ? it.Name : it.Action) + ".");
                if (it.Action == "Stop all") Engine.StopAll();
                else if (it.Action == "Launch all") LaunchAllNow();
                else
                {
                    Preset p = Store.Presets.FirstOrDefault(x => x.Name == it.Preset);
                    if (p != null) RunPreset(p); else Log.Add("Schedule: the preset " + it.Preset + " no longer exists.");
                }
            }
        }

        public void LaunchAllNow()
        {
            if (Store.Accounts.Count > 0) LaunchList(Store.Accounts.OrderBy(x => x.Order).ToList());
        }

        static bool GoodTime(string t)
        {
            DateTime d;
            return t.Length == 5 && DateTime.TryParseExact(t, "HH:mm", null, System.Globalization.DateTimeStyles.None, out d);
        }

        static string DaysText(int days)
        {
            if (days == 127) return "every day";
            if (days == 31) return "weekdays";
            if (days == 96) return "weekends";
            return string.Join(" ", Enumerable.Range(0, 7).Where(i => ((days >> i) & 1) == 1).Select(i => DayNames[i]).ToArray());
        }

        void DrawSchedule(Ui ui, RectangleF a)
        {
            RectangleF view = new RectangleF(a.X - 8, a.Y + 82, a.Width + 16, a.Height - 82);
            List<Preset> presets = Store.Presets;
            int count = Store.Cfg.Schedule.Count;
            ui.BeginScroll(view, 330 + Math.Max(1, count) * 70 + 40, Scroll("schedule"));
            float y = view.Y + 8;
            RectangleF f = new RectangleF(a.X, y, a.Width, 300);
            ui.Glass(f, 18, 0);
            ui.Text("New schedule", f.X + 28, f.Y + 22, Fonts.H2, Pal.Text);
            ui.Text("Runs while Fleet is open (the tray keeps it open).", f.X + 28, f.Y + 52, Fonts.Small, Pal.Muted);
            float x = f.X + 28, fy = f.Y + 84;
            schName = ui.TextField("sc.name", new RectangleF(x, fy, 300, 42), schName, "Name (optional)");
            schTime = ui.TextField("sc.time", new RectangleF(x + 312, fy, 120, 42), schTime, "HH:mm");
            schAction = ui.Segmented("sc.act", new RectangleF(x + 448, fy, f.Width - 56 - 448, 42), SchActions, schAction);
            fy += 58;
            for (int i = 0; i < 7; i++)
            {
                bool on = ((schDays >> i) & 1) == 1;
                RectangleF d = new RectangleF(x + i * 64, fy, 56, 36);
                bool over = ui.Over(d);
                if (over) ui.WantHand = true;
                ui.Fill(d, 9, on ? Pal.A : Pal.Panel2);
                ui.Stroke(d, 9, on ? Pal.A : Pal.Line, 1f);
                ui.TextIn(DayNames[i], d, Fonts.BodyB, on ? Pal.OnAccent : Pal.Muted, StringAlignment.Center);
                if (ui.Pressed && over) ui.Active = "scd" + i;
                if (ui.Released && ui.Active == "scd" + i) { ui.Active = null; if (over) schDays ^= 1 << i; }
            }
            if (schAction == 0)
            {
                string[] names = presets.Select(p => p.Name).ToArray();
                if (schPreset >= names.Length) schPreset = -1;
                schPreset = ui.Picker("sc.pre", new RectangleF(x + 470, fy - 3, f.Width - 56 - 470, 42), names.Length > 0 ? names : new string[] { "(save a preset first)" }, schPreset, "Preset to launch");
            }
            fy += 62;
            string problem = !GoodTime(schTime) ? "Use a time like 20:30." : (schDays == 0 ? "Pick at least one day." : (schAction == 0 && (schPreset < 0 || schPreset >= presets.Count) ? "Pick a preset." : ""));
            if (ui.Button("sc.add", new RectangleF(x, fy, 180, 44), "Add schedule", Btn.Primary, "plus") && problem.Length == 0)
            {
                Store.Cfg.Schedule.Add(new ScheduleItem { Name = schName.Trim(), Time = schTime, Days = schDays, Action = SchActions[schAction], Preset = schAction == 0 ? presets[schPreset].Name : "" });
                Store.SaveSettings(); schName = "";
            }
            if (problem.Length > 0) ui.Text(problem, x + 196, fy + 14, Fonts.Small, Pal.Warn);
            y = f.Bottom + 20;

            if (count == 0) ui.Text("Nothing scheduled yet.", a.X + 4, y + 10, Fonts.Body, Pal.Muted);
            foreach (ScheduleItem it in Store.Cfg.Schedule.ToList())
            {
                RectangleF r = new RectangleF(a.X, y, a.Width, 58);
                ui.Glass(r, 14, 0);
                ui.Text(it.Time, r.X + 24, r.Y + 14, Fonts.Get(22, true), Pal.Text);
                ui.Text(Ui.Fit(ui, (it.Name.Length > 0 ? it.Name + "  -  " : "") + it.Action + (it.Preset.Length > 0 ? ": " + it.Preset : ""), Fonts.BodyB, r.Width - 480), r.X + 130, r.Y + 10, Fonts.BodyB, Pal.Text);
                ui.Text(DaysText(it.Days), r.X + 130, r.Y + 32, Fonts.Small, Pal.Muted);
                bool en = ui.Toggle("sc.t" + it.GetHashCode(), new RectangleF(r.Right - 24 - 36 - 60, r.Y + 16, 46, 26), it.Enabled);
                if (en != it.Enabled) { it.Enabled = en; Store.SaveSettings(); }
                if (ui.Button("sc.d" + it.GetHashCode(), new RectangleF(r.Right - 24 - 36, r.Y + 11, 36, 36), "", Btn.Quiet, "trash")) { Store.Cfg.Schedule.Remove(it); Store.SaveSettings(); break; }
                y += 66;
            }
            ui.EndScroll();
        }
    }
}
