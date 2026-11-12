using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Fleet
{
    // finds the process of every client that is starting, all with one shared loop.
    // New Roblox processes are spotted cheaply; only then is one WMI query made to read their command lines.
    public static class Detector
    {
        class Wait { public long Tracker; public string Needle; public TaskCompletionSource<int> Tcs = new TaskCompletionSource<int>(); }

        static readonly HashSet<int> known = new HashSet<int>();
        static readonly List<Wait> waits = new List<Wait>();
        static readonly Dictionary<int, int> misses = new Dictionary<int, int>();
        static bool looping;

        // everything that is already running is not ours
        public static void Reset()
        {
            known.Clear(); misses.Clear();
            foreach (Process p in Process.GetProcessesByName("RobloxPlayerBeta")) { known.Add(p.Id); p.Dispose(); }
            foreach (Running r in Engine.Live) known.Add(r.Pid);
        }

        public static Task<int> Expect(long tracker)
        {
            Wait w = new Wait { Tracker = tracker, Needle = tracker.ToString() };
            waits.Add(w);
            Loop();
            return w.Tcs.Task;
        }

        public static void Forget(long tracker) { waits.RemoveAll(w => w.Tracker == tracker); }

        static async void Loop()
        {
            if (looping) return;
            looping = true;
            try
            {
                while (waits.Count > 0)
                {
                    await Task.Delay(90);
                    try { await Scan(); } catch (Exception) { }
                }
            }
            finally { looping = false; }
        }

        static async Task Scan()
        {
            List<int> fresh = new List<int>();
            foreach (Process p in Process.GetProcessesByName("RobloxPlayerBeta")) { if (!known.Contains(p.Id)) fresh.Add(p.Id); p.Dispose(); }
            if (fresh.Count == 0) return;
            List<KeyValuePair<int, string>> snap = await Engine.FreshSnapshot();
            foreach (int pid in fresh)
            {
                string cmd = null;
                foreach (KeyValuePair<int, string> kv in snap) if (kv.Key == pid) cmd = kv.Value;
                if (cmd != null)
                {
                    known.Add(pid);
                    Wait w = waits.FirstOrDefault(x => cmd.IndexOf(x.Needle, StringComparison.Ordinal) >= 0);
                    if (w != null) { waits.Remove(w); w.Tcs.TrySetResult(pid); }
                }
                else
                {
                    // the command line could not be read (yet): after a few tries give it to the oldest waiting launch
                    int n; misses.TryGetValue(pid, out n); misses[pid] = ++n;
                    if (n >= 6 && waits.Count > 0) { known.Add(pid); Wait w = waits[0]; waits.RemoveAt(0); w.Tcs.TrySetResult(pid); }
                }
            }
        }
    }
}
