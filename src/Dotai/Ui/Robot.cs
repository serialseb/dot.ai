namespace Dotai.Ui;

public static class Robot
{
    private static readonly byte[] Art = """
           [■_■]
          /|___|\
         /_|   |_\
         |  📖  |
         |______|
          /    \
         /      \
        /________\

        """u8.ToArray();

    private static ReadOnlySpan<byte> ClearScreen => "\x1b[2J\x1b[H"u8;

    public static void ShowIfTty()
    {
        if (!Stdio.IsTty(1)) return;
        Stdio.Write(1, Art);
        Thread.Sleep(1000);
        Stdio.Write(1, ClearScreen);
    }
}
