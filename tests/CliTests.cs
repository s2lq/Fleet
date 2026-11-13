using System;
using System.Linq;
using Fleet;

class CliTests
{
    static int fails;
    static void Chk(bool c, string n) { Console.WriteLine((c ? "PASS " : "FAIL ") + n); if (!c) fails++; }

    public static int Run()
    {
        Opts o = Cli.Parse(new string[] { "launch", "nova", "--place", "123", "--wait", "--group=Farm", "--json" });
        Chk(o.Args.SequenceEqual(new string[] { "launch", "nova" }), "words stay, options come out");
        Chk(o.Get("place", "") == "123" && o.Get("group", "") == "Farm" && o.Has("wait") && o.Has("json"), "options with and without values");

        Chk(Cli.Dispatch(new string[] { "nonsense" }, "").Code == 1, "unknown command fails");
        Chk(Cli.Dispatch(new string[] { "help" }, "").Out.Contains("launch"), "help lists launch");
        Chk(Cli.Dispatch(new string[] { "help", "account" }, "").Out.Contains("account add"), "help account lists its commands");
        Chk(Cli.Dispatch(new string[] { "--help" }, "").Out.Contains("fleetctl"), "--help gives the help");
        Chk(Cli.Dispatch(new string[] { "config", "get", "Theme" }, "").Out == Store.Cfg.Theme, "config get reads a setting");
        Chk(Cli.Dispatch(new string[] { "config", "get", "NoSuchThing" }, "").Code == 1, "config get on a missing key fails");
        Chk(Cli.Dispatch(new string[] { "config", "list", "Opt" }, "").Out.Contains("OptCursor"), "config list filters");
        Chk(!Cli.Dispatch(new string[] { "config", "list", "--json" }, "").Out.Contains("Webhook\",\"value\":\"https"), "secrets are not printed");

        Store.Accounts.Clear();
        Store.Accounts.Add(new Account { Name = "nova_farm", Order = 0 });
        Store.Accounts.Add(new Account { Name = "nova_trade", Order = 1 });
        Store.Accounts.Add(new Account { Name = "kiwi", Order = 2 });
        Chk(Cli.Dispatch(new string[] { "account", "info", "kiwi" }, "").Out.Contains("kiwi"), "an account is found by name");
        Chk(Cli.Dispatch(new string[] { "account", "info", "nova" }, "").Code == 1, "a name that fits two accounts is refused");
        Chk(Cli.Dispatch(new string[] { "account", "info", "nova*" }, "").Out.Contains("nova_trade"), "a * picks several");
        Chk(Cli.Dispatch(new string[] { "account", "info", "nobody" }, "").Code == 1, "an unknown account fails");
        Chk(Cli.Dispatch(new string[] { "account", "remove", "kiwi" }, "").Code == 1 && Store.Accounts.Count == 3, "remove needs --yes");
        Chk(Cli.Dispatch(new string[] { "account", "list", "--json" }, "").Out.StartsWith("[{"), "--json gives json");
        return fails;
    }
}
