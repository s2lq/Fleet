using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

namespace Fleet
{
    // Discord Rich Presence over Discord's local pipe. It needs the Application ID of an app you made at discord.com/developers.
    public static class Rpc
    {
        static NamedPipeClientStream pipe;
        static string connectedId = "";
        static long started;
        static bool busy;
        public static string Status = "";

        public static async void Update(int running, int total)
        {
            if (busy) return;
            busy = true;
            try { await Task.Run(delegate { Send(running, total); }); }
            catch (Exception ex) { Status = ex.Message; Close(); }
            busy = false;
        }

        static void Close()
        {
            try { if (pipe != null) pipe.Dispose(); } catch (Exception) { }
            pipe = null; connectedId = "";
        }

        static void Write(int op, string json)
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            byte[] frame = new byte[8 + body.Length];
            BitConverter.GetBytes(op).CopyTo(frame, 0);
            BitConverter.GetBytes(body.Length).CopyTo(frame, 4);
            body.CopyTo(frame, 8);
            pipe.Write(frame, 0, frame.Length);
            pipe.Flush();
        }

        static string Read()
        {
            byte[] head = new byte[8];
            int n = 0;
            while (n < 8) { int r = pipe.Read(head, n, 8 - n); if (r <= 0) throw new IOException("Discord closed the connection."); n += r; }
            int len = BitConverter.ToInt32(head, 4);
            byte[] body = new byte[len];
            n = 0;
            while (n < len) { int r = pipe.Read(body, n, len - n); if (r <= 0) throw new IOException("Discord closed the connection."); n += r; }
            return Encoding.UTF8.GetString(body);
        }

        static bool Connect(string id)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    NamedPipeClientStream p = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut, PipeOptions.None);
                    p.Connect(250);
                    pipe = p;
                    Write(0, "{\"v\":1,\"client_id\":\"" + id + "\"}");
                    Read();
                    connectedId = id;
                    started = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                    return true;
                }
                catch (Exception) { Close(); }
            }
            return false;
        }

        static void Send(int running, int total)
        {
            string id = (Store.Cfg.DiscordAppId ?? "").Trim();
            bool on = Store.Cfg.RichPresence && id.Length > 5;
            if (!on)
            {
                if (pipe != null) { try { Write(1, Activity(null, 0, 0)); Read(); } catch (Exception) { } Close(); }
                Status = Store.Cfg.RichPresence ? "Enter your Discord Application ID." : "";
                return;
            }
            if (pipe == null || connectedId != id)
            {
                Close();
                if (!Connect(id)) { Status = "Discord is not running."; return; }
            }
            Write(1, Activity("x", running, total));
            Read();
            Status = "Connected.";
        }

        static string Activity(string on, int running, int total)
        {
            string act = on == null ? "null" :
                "{\"details\":\"" + (running > 0 ? running + (running == 1 ? " account running" : " accounts running") : "Idle") + "\",\"state\":\"" + total + " accounts saved\",\"timestamps\":{\"start\":" + started + "}}";
            return "{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":" + Process.GetCurrentProcess().Id + ",\"activity\":" + act + "},\"nonce\":\"" + Guid.NewGuid().ToString("N") + "\"}";
        }
    }
}
