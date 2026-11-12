using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Fleet
{
    // cpu limit, memory limit, throttling of background windows, anti-afk
    public static class Perf
    {
        static IntPtr totalJob = IntPtr.Zero;
        static readonly HashSet<int> inTotal = new HashSet<int>();
        static readonly HashSet<int> capped = new HashSet<int>();
        static readonly Dictionary<int, DateTime> afk = new Dictionary<int, DateTime>();

        static IntPtr MakeJob(int percent)
        {
            IntPtr job = Native.CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero) return job;
            Native.CpuRate info = new Native.CpuRate { ControlFlags = 1 | 4, CpuRatePercent = (uint)(Math.Max(1, Math.Min(100, percent)) * 100) };
            Native.SetInformationJobObject(job, 15, ref info, Marshal.SizeOf(typeof(Native.CpuRate)));
            return job;
        }

        public static void Apply()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            Settings c = Store.Cfg;
            foreach (Running r in Engine.Live.ToList())
            {
                try
                {
                    using (Process p = Process.GetProcessById(r.Pid))
                    {
                        if (c.CpuCapMode == "Each" && capped.Add(r.Pid))
                        {
                            IntPtr job = MakeJob(c.CpuCapPercent);
                            if (job != IntPtr.Zero) Native.AssignProcessToJobObject(job, p.Handle);
                        }
                        else if (c.CpuCapMode == "Total" && inTotal.Add(r.Pid))
                        {
                            if (totalJob == IntPtr.Zero) totalJob = MakeJob(c.CpuCapPercent);
                            if (totalJob != IntPtr.Zero) Native.AssignProcessToJobObject(totalJob, p.Handle);
                        }
                        if (c.RamCapMode != "Off")
                        {
                            long mb = c.RamCapMode == "Fixed500" ? 500 : 250;
                            Native.SetProcessWorkingSetSizeEx(p.Handle, (IntPtr)(-1), (IntPtr)(mb << 20), 2 | 4);
                        }
                    }
                }
                catch (Exception) { }
            }
        }

        // job objects can only be changed by making a new job; processes already inside keep their old cap until restart
        public static void CapChanged() { capped.Clear(); inTotal.Clear(); totalJob = IntPtr.Zero; }

        public static void Background()
        {
            if (!Store.Cfg.ThrottleBackground || Engine.Live.Count < 2) return;
            IntPtr fg = Native.GetForegroundWindow();
            uint fgPid;
            Native.GetWindowThreadProcessId(fg, out fgPid);
            foreach (Running r in Engine.Live)
            {
                try
                {
                    using (Process p = Process.GetProcessById(r.Pid))
                    {
                        bool front = (uint)r.Pid == fgPid;
                        p.PriorityClass = front ? ProcessPriorityClass.Normal : ProcessPriorityClass.Idle;
                        if (!front) Native.EmptyWorkingSet(p.Handle);
                    }
                }
                catch (Exception) { }
            }
        }

        public static void AntiAfk()
        {
            if (!Store.Cfg.AntiAfk) return;
            HashSet<uint> pids = new HashSet<uint>(Engine.Live.Select(r => (uint)r.Pid));
            Dictionary<uint, IntPtr> wins = Native.WindowsOf(pids);
            foreach (KeyValuePair<uint, IntPtr> kv in wins)
            {
                DateTime last;
                if (afk.TryGetValue((int)kv.Key, out last) && (DateTime.Now - last).TotalMinutes < Store.Cfg.AntiAfkMin) continue;
                afk[(int)kv.Key] = DateTime.Now;
                Native.PostMessage(kv.Value, 0x100, (IntPtr)0x10, IntPtr.Zero);
                Native.PostMessage(kv.Value, 0x101, (IntPtr)0x10, IntPtr.Zero);
            }
        }

        // ---------- graphics
        static readonly Dictionary<string, int> Levels = new Dictionary<string, int> { { "Lowest", 1 }, { "Low", 3 }, { "Light", 5 }, { "Default", 0 }, { "High", 15 }, { "Best", 21 } };
        public static readonly string[] GfxNames = { "Default", "Lowest", "Low", "Light", "High", "Best" };

        public static void SetGraphics(string preset)
        {
            int lvl;
            if (!Levels.TryGetValue(preset, out lvl) || lvl == 0) return;
            try
            {
                string f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "GlobalBasicSettings_13.xml");
                if (!File.Exists(f)) return;
                string xml = File.ReadAllText(f);
                xml = Put(xml, "int", "GraphicsQualityLevel", lvl.ToString());
                xml = Put(xml, "token", "SavedQualityLevel", Math.Min(10, lvl).ToString());
                File.WriteAllText(f, xml);
            }
            catch (Exception ex) { Log.Add("Graphics could not be written: " + ex.Message); }
        }

        static string Put(string xml, string type, string name, string val)
        {
            Regex re = new Regex("(<" + type + " name=\"" + name + "\">)[^<]*(</" + type + ">)");
            if (re.IsMatch(xml)) return re.Replace(xml, "${1}" + val + "${2}");
            return xml.Replace("</Properties>", "<" + type + " name=\"" + name + "\">" + val + "</" + type + "></Properties>");
        }
    }

    // mute single processes through the Windows audio sessions
    public static class Audio
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class Enumerator { }
        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator { int EnumAudioEndpoints(int a, int b, out object c); int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice dev); }
        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice { int Activate(ref Guid iid, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o); }
        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionManager2 { int f1(); int f2(); int GetSessionEnumerator(out IAudioSessionEnumerator e); }
        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionEnumerator { int GetCount(out int n); int GetSession(int i, out IAudioSessionControl2 s); }
        [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionControl2
        {
            int f1(); int f2(); int f3(); int f4(); int f5(); int f6(); int f7(); int f8(); int f9(); int f10(); int f11();
            int GetProcessId(out uint pid);
        }
        [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ISimpleAudioVolume { int SetMasterVolume(float v, ref Guid ctx); int GetMasterVolume(out float v); int SetMute(bool m, ref Guid ctx); int GetMute(out bool m); }

        public static void SetMute(int pid, bool mute)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            try
            {
                IMMDeviceEnumerator en = (IMMDeviceEnumerator)new Enumerator();
                IMMDevice dev; en.GetDefaultAudioEndpoint(0, 1, out dev);
                Guid iid = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
                object o; dev.Activate(ref iid, 23, IntPtr.Zero, out o);
                IAudioSessionManager2 mgr = (IAudioSessionManager2)o;
                IAudioSessionEnumerator se; mgr.GetSessionEnumerator(out se);
                int n; se.GetCount(out n);
                for (int i = 0; i < n; i++)
                {
                    IAudioSessionControl2 s; se.GetSession(i, out s);
                    uint spid; s.GetProcessId(out spid);
                    if (spid == (uint)pid)
                    {
                        Guid ctx = Guid.Empty;
                        ((ISimpleAudioVolume)s).SetMute(mute, ref ctx);
                    }
                }
            }
            catch (Exception) { }
        }
    }
}
