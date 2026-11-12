using System;
using System.Linq;
using Fleet;

class ChangesTests
{
    static int fails;
    static void Chk(bool c, string n) { Console.WriteLine((c ? "PASS " : "FAIL ") + n); if (!c) fails++; }

    public static int Run()
    {
        var list = Changes.Parse("# Changelog\n\n## 1.2.0\n\n- first\n- second\n\n## 1.1.0\n\n- old\n");
        Chk(list.Count == 2 && list[0].Version == "1.2.0" && list[0].Items.Count == 2, "parses versions and bullets");
        Chk(Changes.Newer("1.10.0", "1.9.0") && !Changes.Newer("1.0.0", "1.0.0"), "compares versions as numbers");
        Chk(Changes.Parse("").Count == 0 && Changes.Parse(null).Count == 0, "empty text gives nothing");
        return fails;
    }
}
