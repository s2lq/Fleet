using System;

class Runner
{
    static int Main()
    {
        int fails = TextFieldTests.Run() + LayoutTests.Run();
        Console.WriteLine(fails == 0 ? "ALL PASSED" : fails + " FAILED");
        return fails;
    }
}
