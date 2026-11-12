using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Fleet
{
    // commands from outside: the command line, fleet:// links, and a second copy of Fleet handing its job to the first
    public static class Cmd
    {
        const string Pipe = "Fleet_Cmd";

        // turns the arguments into one line, or "" when there is nothing to do
        public static string FromArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.StartsWith("fleet://", StringComparison.OrdinalIgnoreCase)) return a;
                if (a == "--launch-preset" && i + 1 < args.Length) return "launch-preset:" + args[i + 1];
                if (a == "--launch-all") return "launch-all";
                if (a == "--stop-all") return "stop-all";
            }
            return "";
        }

        // false when no other Fleet is listening
        public static bool Send(string line)
        {
            try
            {
                using (NamedPipeClientStream c = new NamedPipeClientStream(".", Pipe, PipeDirection.Out))
                {
                    c.Connect(1500);
                    byte[] b = System.Text.Encoding.UTF8.GetBytes(line + "\n");
                    c.Write(b, 0, b.Length);
                    c.Flush();
                }
                return true;
            }
            catch (Exception) { return false; }
        }

        public static void Listen(Control ui)
        {
            Thread t = new Thread(delegate()
            {
                while (true)
                {
                    try
                    {
                        using (NamedPipeServerStream s = new NamedPipeServerStream(Pipe, PipeDirection.In, 1, PipeTransmissionMode.Byte))
                        {
                            s.WaitForConnection();
                            string line = new StreamReader(s, System.Text.Encoding.UTF8).ReadLine();
                            if (!string.IsNullOrEmpty(line)) ui.BeginInvoke((MethodInvoker)delegate { Run(line); });
                        }
                    }
                    catch (Exception) { Thread.Sleep(500); }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        public static void Run(string line)
        {
            MainForm f = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
            if (f == null) return;
            App app = f.View.AppRef;
            line = (line ?? "").Trim();
            if (line.StartsWith("fleet://", StringComparison.OrdinalIgnoreCase))
            {
                string rest = Uri.UnescapeDataString(line.Substring(8)).Trim('/');
                if (rest.StartsWith("launch/", StringComparison.OrdinalIgnoreCase)) line = "launch-preset:" + rest.Substring(7);
                else if (rest.Equals("launchall", StringComparison.OrdinalIgnoreCase)) line = "launch-all";
                else if (rest.Equals("stopall", StringComparison.OrdinalIgnoreCase)) line = "stop-all";
                else line = "show";
            }
            if (line == "show") f.ShowMain();
            else if (line == "launch-all") app.LaunchAllNow();
            else if (line == "stop-all") Engine.StopAll();
            else if (line.StartsWith("launch-preset:"))
            {
                string name = line.Substring(14).Trim();
                Preset p = Store.Presets.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (p != null) app.RunPreset(p); else Log.Add("No preset called " + name + ".");
            }
        }
    }
}
