using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Fleet
{
    // frees memory for the whole PC: empties working sets and the standby and modified page lists (needs administrator)
    public static class MemClean
    {
        [StructLayout(LayoutKind.Sequential)]
        struct MemStatus { public uint Length, Load; public ulong TotalPhys, AvailPhys, TotalPage, AvailPage, TotalVirt, AvailVirt, AvailExt; }
        [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MemStatus s);
        [DllImport("ntdll.dll")] static extern int NtSetSystemInformation(int cls, ref int info, int len);
        [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr p, uint access, out IntPtr token);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern bool LookupPrivilegeValue(string sys, string name, out long luid);
        [DllImport("advapi32.dll", SetLastError = true)] static extern bool AdjustTokenPrivileges(IntPtr token, bool disable, ref TokPriv state, int len, IntPtr prev, IntPtr ret);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct TokPriv { public int Count; public long Luid; public int Attr; }

        public static bool IsAdmin()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return false;
            try { using (WindowsIdentity id = WindowsIdentity.GetCurrent()) return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator); }
            catch (Exception) { return false; }
        }

        public static bool Status(out int load, out long total, out long avail)
        {
            load = 0; total = 0; avail = 0;
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) { load = 50; total = 16L << 30; avail = 8L << 30; return true; }
            MemStatus s = new MemStatus { Length = (uint)Marshal.SizeOf(typeof(MemStatus)) };
            if (!GlobalMemoryStatusEx(ref s)) return false;
            load = (int)s.Load; total = (long)s.TotalPhys; avail = (long)s.AvailPhys;
            return true;
        }

        static void Privilege(string name)
        {
            IntPtr token;
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x28, out token)) return;
            long luid;
            if (LookupPrivilegeValue(null, name, out luid))
            {
                TokPriv tp = new TokPriv { Count = 1, Luid = luid, Attr = 2 };
                AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            CloseHandle(token);
        }

        // returns an error text, or null when it worked
        public static string Clean()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return "Windows only.";
            if (!IsAdmin()) return "Needs administrator rights.";
            Privilege("SeProfileSingleProcessPrivilege");
            Privilege("SeIncreaseQuotaPrivilege");
            int[] steps = { 2, 3, 4 };      // empty working sets, flush modified pages, purge the standby list
            foreach (int cmd in steps)
            {
                int c = cmd;
                NtSetSystemInformation(80, ref c, sizeof(int));
            }
            return null;
        }

        // asks Windows for administrator rights for one clean
        public static string CleanElevated()
        {
            try
            {
                Process p = Process.Start(new ProcessStartInfo(System.Windows.Forms.Application.ExecutablePath, "--memclean") { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
                if (p != null) p.WaitForExit(15000);
                return null;
            }
            catch (Exception) { return "Cancelled."; }
        }

        public static void RestartAsAdmin()
        {
            try
            {
                Process.Start(new ProcessStartInfo(System.Windows.Forms.Application.ExecutablePath) { UseShellExecute = true, Verb = "runas" });
                System.Windows.Forms.Application.Exit();
            }
            catch (Exception) { }
        }
    }
}
