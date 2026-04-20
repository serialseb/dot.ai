namespace Dotai.Ui;

public static class Robot
{
    private const string Art = """
           [■_■]
          /|___|\
         /_|   |_\
         |  📖  |
         |______|
          /    \
         /      \
        /________\
        """;

    public static void ShowIfTty()
    {
        if (Console.IsOutputRedirected) return;
        Console.WriteLine(Art);
        Thread.Sleep(1000);
        Console.Write("\x1b[2J\x1b[H");
    }
}
