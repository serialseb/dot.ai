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
        if (Stdio.IsTty(Stdout))
        {
            Stdio.Write(Stdout, "\x1b[32m✨ "u8);
            Stdio.Write(Stdout, msg);
            Stdio.Write(Stdout, "\x1b[0m\n"u8);
        }
        else
        {
            Stdio.Write(Stdout, "success: "u8);
            Stdio.Write(Stdout, msg);
            Stdio.Write(Stdout, "\n"u8);
        }
    }

    public static void Hint(ReadOnlySpan<byte> msg)
    {
        if (Stdio.IsTty(Stdout))
        {
            Stdio.Write(Stdout, "\x1b[36m\xe2\x84\xb9\xef\xb8\x8f  "u8);
            Stdio.Write(Stdout, msg);
            Stdio.Write(Stdout, "\x1b[0m\n"u8);
        }
        else
        {
            Stdio.Write(Stdout, "hint: "u8);
            Stdio.Write(Stdout, msg);
            Stdio.Write(Stdout, "\n"u8);
        }
    }

    public static void Warn(ReadOnlySpan<byte> msg)
    {
        if (Stdio.IsTty(Stderr))
        {
            Stdio.Write(Stderr, "\x1b[33m\xe2\x9a\xa0\xef\xb8\x8f  warn: "u8);
            Stdio.Write(Stderr, msg);
            Stdio.Write(Stderr, "\x1b[0m\n"u8);
        }
        else
        {
            Stdio.Write(Stderr, "warn: "u8);
            Stdio.Write(Stderr, msg);
            Stdio.Write(Stderr, "\n"u8);
        }
    }

    public static void Error(ReadOnlySpan<byte> msg)
    {
        if (Stdio.IsTty(Stderr))
        {
            Stdio.Write(Stderr, "\x1b[31m\xe2\x9d\x8c error: "u8);
            Stdio.Write(Stderr, msg);
            Stdio.Write(Stderr, "\x1b[0m\n"u8);
        }
        else
        {
            Stdio.Write(Stderr, "error: "u8);
            Stdio.Write(Stderr, msg);
            Stdio.Write(Stderr, "\n"u8);
        }
    }

    public static void Detail(ReadOnlySpan<byte> msg)
    {
        Stdio.Write(Stderr, msg);
        Stdio.Write(Stderr, "\n"u8);
    }
}
