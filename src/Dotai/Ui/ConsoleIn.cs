using Dotai.Services;

namespace Dotai.Ui;

public static unsafe class ConsoleIn
{
    // Reads from stdin until a newline or EOF and returns true if the first
    // non-whitespace byte is 'y' or 'Y'. Any other response — including empty,
    // EOF, or closed stream — returns false, so callers treat "no input" as a
    // decline rather than a stall. Reads character-by-character to avoid
    // buffering issues when stdin is a TTY.
    public static bool ReadYesNo()
    {
        bool sawChar = false;
        bool answerYes = false;
        byte one;
        while (true)
        {
            long n = Libc.Read(0, &one, 1);
            if (n <= 0) return sawChar && answerYes;
            if (one == (byte)'\n' || one == (byte)'\r') return answerYes;
            if (!sawChar && (one == (byte)' ' || one == (byte)'\t')) continue;
            if (!sawChar)
            {
                sawChar = true;
                answerYes = one == (byte)'y' || one == (byte)'Y';
            }
        }
    }
}
