using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;

namespace Fleet
{
    public class MacroStep { public int Dt; public int Kind; public int A, B; }

    // record mouse and keyboard, play them back (like TinyTask)
    public static class Macro
    {
        delegate IntPtr Hook(int code, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, Hook cb, IntPtr mod, uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string n);
        [StructLayout(LayoutKind.Sequential)] struct MsLL { public int X, Y; public uint Data, Flags, Time; public IntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] struct KbLL { public uint VK, Scan, Flags, Time; public IntPtr Extra; }

        public static List<MacroStep> Steps = new List<MacroStep>();
        public static bool Recording, Playing;
        static IntPtr hm, hk;
        static Hook mouseCb, keyCb;
        static DateTime last;
        static Thread player;
        static volatile bool stopPlay;

        // kinds: 0 move, 1 left down, 2 left up, 3 right down, 4 right up, 5 key down, 6 key up
        static void Add(int kind, int a, int b)
        {
            DateTime now = DateTime.Now;
            Steps.Add(new MacroStep { Dt = (int)(now - last).TotalMilliseconds, Kind = kind, A = a, B = b });
            last = now;
        }

        public static void Record()
        {
            if (Recording || Playing || Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            Steps = new List<MacroStep>();
            last = DateTime.Now;
            Recording = true;
            mouseCb = delegate(int code, IntPtr w, IntPtr l)
            {
                if (code >= 0 && Recording)
                {
                    MsLL m = (MsLL)Marshal.PtrToStructure(l, typeof(MsLL));
                    int msg = (int)w;
                    if (msg == 0x200) { if (Steps.Count == 0 || (DateTime.Now - last).TotalMilliseconds > 15) Add(0, m.X, m.Y); }
                    else if (msg == 0x201) Add(1, m.X, m.Y); else if (msg == 0x202) Add(2, m.X, m.Y);
                    else if (msg == 0x204) Add(3, m.X, m.Y); else if (msg == 0x205) Add(4, m.X, m.Y);
                }
                return CallNextHookEx(hm, code, w, l);
            };
            keyCb = delegate(int code, IntPtr w, IntPtr l)
            {
                if (code >= 0 && Recording)
                {
                    KbLL k = (KbLL)Marshal.PtrToStructure(l, typeof(KbLL));
                    int msg = (int)w;
                    if (msg == 0x100 || msg == 0x104) Add(5, (int)k.VK, (int)k.Scan); else if (msg == 0x101 || msg == 0x105) Add(6, (int)k.VK, (int)k.Scan);
                }
                return CallNextHookEx(hk, code, w, l);
            };
            IntPtr mod = GetModuleHandle(null);
            hm = SetWindowsHookEx(14, mouseCb, mod, 0);
            hk = SetWindowsHookEx(13, keyCb, mod, 0);
        }

        public static void StopRecord()
        {
            Recording = false;
            if (hm != IntPtr.Zero) UnhookWindowsHookEx(hm);
            if (hk != IntPtr.Zero) UnhookWindowsHookEx(hk);
            hm = hk = IntPtr.Zero;
            if (Steps.Count > 0) Steps[0].Dt = 0;
        }

        // loops = 0 means until stopped
        public static void Play(int loops)
        {
            if (Playing || Recording || Steps.Count == 0) return;
            Playing = true; stopPlay = false;
            List<MacroStep> copy = new List<MacroStep>(Steps);
            player = new Thread(delegate()
            {
                for (int n = 0; (loops == 0 || n < loops) && !stopPlay; n++)
                {
                    foreach (MacroStep s in copy)
                    {
                        if (stopPlay) break;
                        if (s.Dt > 0) Thread.Sleep(s.Dt);
                        switch (s.Kind)
                        {
                            case 0: Native.SetCursorPos(s.A, s.B); break;
                            case 1: Native.SetCursorPos(s.A, s.B); Native.mouse_event(0x2, 0, 0, 0, UIntPtr.Zero); break;
                            case 2: Native.mouse_event(0x4, 0, 0, 0, UIntPtr.Zero); break;
                            case 3: Native.SetCursorPos(s.A, s.B); Native.mouse_event(0x8, 0, 0, 0, UIntPtr.Zero); break;
                            case 4: Native.mouse_event(0x10, 0, 0, 0, UIntPtr.Zero); break;
                            case 5: Native.keybd_event((byte)s.A, (byte)s.B, 0, UIntPtr.Zero); break;
                            case 6: Native.keybd_event((byte)s.A, (byte)s.B, 2, UIntPtr.Zero); break;
                        }
                    }
                }
                Playing = false;
            });
            player.IsBackground = true;
            player.Start();
        }

        public static void StopPlay() { stopPlay = true; }

        public static void Save(string file) { File.WriteAllText(file, new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(Steps)); }
        public static void Load(string file) { Steps = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<List<MacroStep>>(File.ReadAllText(file)); }
    }
}
