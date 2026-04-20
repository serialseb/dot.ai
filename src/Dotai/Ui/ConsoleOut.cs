namespace Dotai.Ui;

public static class ConsoleOut
{
    private const int Stdout = 1;
    private const int Stderr = 2;

    public static void Info(ReadOnlySpan<byte> msg)
    {
        Stdio.Write(Stdout, msg);
        Stdio.Write(Stdout, "\n"u8);
    }

    public static void Success(ReadOnlySpan<byte> msg)
    {
        Stdio.Write(Stdout, "✨ "u8);
        Stdio.Write(Stdout, msg);
        Stdio.Write(Stdout, "\n"u8);
    }

    public static void Hint(ReadOnlySpan<byte> msg)
    {
        Stdio.Write(Stdout, "ℹ️  "u8);
        Stdio.Write(Stdout, msg);
        Stdio.Write(Stdout, "\n"u8);
    }

    public static void Warn(ReadOnlySpan<byte> msg)
    {
        Stdio.Write(Stderr, "⚠️  warn: "u8);
        Stdio.Write(Stderr, msg);
        Stdio.Write(Stderr, "\n"u8);
    }

    public static void Error(ReadOnlySpan<byte> msg)
    {
        Stdio.Write(Stderr, "❌ error: "u8);
        Stdio.Write(Stderr, msg);
        Stdio.Write(Stderr, "\n"u8);
    }

    public static void Detail(ReadOnlySpan<byte> msg)
    {
        Stdio.Write(Stderr, msg);
        Stdio.Write(Stderr, "\n"u8);
    }
}
