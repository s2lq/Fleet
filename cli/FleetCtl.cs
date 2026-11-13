using System;
using System.IO;

namespace Fleet
{
    // fleetctl.exe: a small console program. It waits for the command to finish and gives back an exit code
    // (0 fine, 1 the command failed, 2 timed out, 4 could not reach Fleet), so scripts can use it.
    static class FleetCtl
    {
        static int Main(string[] args)
        {
            string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fleet.exe");
            return CliClient.Run(args, exe);
        }
    }
}
