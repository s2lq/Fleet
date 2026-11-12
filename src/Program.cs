using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Fleet
{
    static class Program
    {
        public const string Version = "1.0.0";
        static Mutex single;

        [STAThread]
        static void Main(string[] args)
        {
            int wp = Array.IndexOf(args, "--wait-pid");
            if (wp >= 0 && wp + 1 < args.Length)
            {
                int pid;
                try { if (int.TryParse(args[wp + 1], out pid)) using (Process p = Process.GetProcessById(pid)) p.WaitForExit(8000); } catch (Exception) { }
            }
            try { string old = Application.ExecutablePath + ".old"; if (File.Exists(old)) File.Delete(old); } catch (Exception) { }

            if (Array.IndexOf(args, "--memclean") >= 0) { MemClean.Clean(); return; }

            foreach (string a in args)
                if (a == "--help" || a == "-h" || a == "/?")
                {
                    MessageBox.Show("Fleet.exe [options]\n\n--launch NAME[,NAME]   launch these accounts (name, alias or display name)\n--place ID             with --launch: the game to join\n--launch-preset NAME   run a saved preset\n--launch-all           launch every account\n--stop-all             close every Roblox window\n--min                  start minimized\n\nLinks: fleet://launch-account/NAME?place=ID, fleet://launch/PRESET, fleet://launchall, fleet://stopall\n\nIf Fleet is already running, the command goes to it.", "Fleet");
                    return;
                }
            string cmd = Cmd.FromArgs(args);
            bool created;
            single = new Mutex(true, "Fleet_SingleInstance", out created);
            if (!created) { Cmd.Send(cmd.Length > 0 ? cmd : "show"); return; }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Crash(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Crash(e.ExceptionObject as Exception); };

            Store.Init();
            Lang.Load(Store.Cfg.Language);
            Themes.Apply(Store.Cfg.Theme);
            if (!Unlock()) return;
            Engine.SetMultiInstance(Store.Cfg.MultiInstance);
            MainForm main = new MainForm(Array.IndexOf(args, "--min") >= 0);
            if (cmd.Length > 0) main.Shown += delegate { Cmd.Run(cmd); };
            Application.Run(main);
            GC.KeepAlive(single);
        }

        static bool Unlock()
        {
            if (!Store.AccountsPasswordProtected()) return Store.LoadAccounts(null) || true;
            for (int tries = 0; tries < 5; tries++)
            {
                string pw = Dialogs.Password(null, Lang.T("Fleet is locked"), Lang.T("Password:"), false);
                if (pw == null) return false;
                if (Store.LoadAccounts(pw)) return true;
                MessageBox.Show(Lang.T("That password is wrong."));
            }
            return false;
        }

        static void Crash(Exception ex)
        {
            try { File.AppendAllText(Path.Combine(Store.Dir ?? Path.GetTempPath(), "crash.txt"), DateTime.Now + "\r\n" + ex + "\r\n\r\n"); } catch (Exception) { }
            Log.Add("Unexpected error: " + (ex != null ? ex.Message : "unknown"));
        }
    }
}
