using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;

namespace Fleet
{
    // the side that runs in the terminal: sends the command to the running Fleet and prints the answer.
    // This file is compiled into Fleet.exe and into fleetctl.exe.
    public static class CliClient
    {
        const string Pipe = "Fleet_Cli";
        const char Sep = '\u001f';
        const string ExitMark = "\u0001EXIT ";

        static NamedPipeClientStream TryConnect(int ms)
        {
            try
            {
                NamedPipeClientStream c = new NamedPipeClientStream(".", Pipe, PipeDirection.InOut);
                c.Connect(ms);
                return c;
            }
            catch (Exception) { return null; }
        }

        // connects to Fleet, starting it (minimized) when it is not running
        static NamedPipeClientStream Connect(string fleetExe)
        {
            NamedPipeClientStream c = TryConnect(400);
            if (c != null) return c;
            if (string.IsNullOrEmpty(fleetExe) || !File.Exists(fleetExe)) { Console.Error.WriteLine("Fleet is not running and Fleet.exe was not found next to this program."); return null; }
            Console.Error.WriteLine("Starting Fleet... (if it asks for your password, unlock it)");
            try { Process.Start(new ProcessStartInfo(fleetExe, "--min") { UseShellExecute = false }); }
            catch (Exception ex) { Console.Error.WriteLine("Could not start Fleet: " + ex.Message); return null; }
            DateTime end = DateTime.UtcNow.AddSeconds(180);
            while (DateTime.UtcNow < end)
            {
                c = TryConnect(700);
                if (c != null) return c;
                Thread.Sleep(300);
            }
            Console.Error.WriteLine("Fleet did not start in time.");
            return null;
        }

        public static int Run(string[] args, string fleetExe)
        {
            try { Console.OutputEncoding = new UTF8Encoding(false); } catch (Exception) { }
            string stdin = "";
            if (args.Contains("--stdin")) { try { stdin = Console.In.ReadToEnd(); } catch (Exception) { } }
            NamedPipeClientStream c = Connect(fleetExe);
            if (c == null) return 4;
            using (c)
            {
                UTF8Encoding enc = new UTF8Encoding(false);
                StreamWriter w = new StreamWriter(c, enc) { AutoFlush = true, NewLine = "\n" };
                w.Write(string.Join(Sep.ToString(), args) + "\n" + Convert.ToBase64String(enc.GetBytes(stdin)) + "\n");
                StreamReader r = new StreamReader(c, enc);
                StringBuilder body = new StringBuilder();
                string line;
                int code = 0;
                bool gotMark = false;
                while ((line = r.ReadLine()) != null)
                {
                    if (line.StartsWith(ExitMark)) { int.TryParse(line.Substring(ExitMark.Length), out code); gotMark = true; break; }
                    body.AppendLine(line);
                }
                string text = body.ToString().TrimEnd('\r', '\n');
                if (text.Length > 0) Console.Out.WriteLine(text);
                Console.Out.Flush();
                return gotMark ? code : 5;
            }
        }
    }
}
