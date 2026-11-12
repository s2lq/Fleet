using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Fleet
{
    static class Native
    {
        public delegate bool EnumProc(IntPtr hwnd, IntPtr lp);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lp);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hgt, uint flags);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("psapi.dll")] public static extern bool EmptyWorkingSet(IntPtr process);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("kernel32.dll")] public static extern IntPtr CreateJobObject(IntPtr attr, string name);
        [DllImport("kernel32.dll")] public static extern bool SetInformationJobObject(IntPtr job, int cls, ref CpuRate info, int size);
        [DllImport("kernel32.dll")] public static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll")] public static extern bool SetProcessWorkingSetSizeEx(IntPtr p, IntPtr min, IntPtr max, uint flags);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int idx, int v);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int idx);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool SetWindowText(IntPtr h, string text);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, int data, UIntPtr extra);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out System.Drawing.Point p);

        [DllImport("imm32.dll")] public static extern IntPtr ImmGetContext(IntPtr hwnd);
        [DllImport("imm32.dll")] public static extern bool ImmReleaseContext(IntPtr hwnd, IntPtr himc);
        [DllImport("imm32.dll")] public static extern bool ImmSetCompositionWindow(IntPtr himc, ref CompForm cf);
        [DllImport("imm32.dll")] public static extern bool ImmSetCandidateWindow(IntPtr himc, ref CandForm cf);
        [StructLayout(LayoutKind.Sequential)] public struct Pt { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct Rc { public int L, T, R, B; }
        [StructLayout(LayoutKind.Sequential)] public struct CompForm { public uint Style; public Pt Pos; public Rc Area; }
        [StructLayout(LayoutKind.Sequential)] public struct CandForm { public uint Index, Style; public Pt Pos; public Rc Area; }

        // puts the input-method composition and candidate windows next to the text caret
        public static void PlaceIme(IntPtr hwnd, int x, int y)
        {
            try
            {
                IntPtr himc = ImmGetContext(hwnd);
                if (himc == IntPtr.Zero) return;
                CompForm cf = new CompForm { Style = 2, Pos = new Pt { X = x, Y = y } };
                ImmSetCompositionWindow(himc, ref cf);
                CandForm cd = new CandForm { Index = 0, Style = 0x40, Pos = new Pt { X = x, Y = y + 4 } };
                ImmSetCandidateWindow(himc, ref cd);
                ImmReleaseContext(hwnd, himc);
            }
            catch (Exception) { }
        }

        [StructLayout(LayoutKind.Sequential)] public struct CpuRate { public uint ControlFlags; public uint CpuRatePercent; }

        public const uint SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;

        public static Dictionary<uint, IntPtr> WindowsOf(HashSet<uint> pids)
        {
            Dictionary<uint, IntPtr> map = new Dictionary<uint, IntPtr>();
            EnumWindows(delegate(IntPtr h, IntPtr lp)
            {
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (pids.Contains(pid) && IsWindowVisible(h) && GetWindowTextLength(h) > 0 && !map.ContainsKey(pid)) map[pid] = h;
                return true;
            }, IntPtr.Zero);
            return map;
        }

        public static void DarkTitleBar(IntPtr hwnd)
        {
            try { int one = Pal.IsLight ? 0 : 1; DwmSetWindowAttribute(hwnd, 20, ref one, 4); }
            catch (Exception) { }
        }
    }
}
