using Dotai.Services;
using Dotai.Text;
using Dotai.Ui;

namespace Dotai.Commands;

public sealed record ParsedArgs(byte[] StartDir, bool Force, Arg[] Positional);

public static class SharedFlags
{
    private enum State
    {
        Normal,
        ExpectingProjectPath,
    }

    public static bool TryParse(Arg[] args, FastString defaultStartDir, out ParsedArgs result)
    {
        var state = State.Normal;
        var startDir = defaultStartDir.Bytes.ToArray();
        var force = false;
        var positional = new List<Arg>();

        foreach (var token in args)
        {
            switch (state)
            {
                case State.Normal:
                    if (token.AsFast == "-p"u8 || token.AsFast == "--project"u8)
                    {
                        state = State.ExpectingProjectPath;
                    }
                    else if (token.AsFast == "-f"u8 || token.AsFast == "--force"u8)
                    {
                        force = true;
                    }
                    else if (IsHelpToken(token))
                    {
                        positional.Add(token);
                    }
                    else if (IsFlagToken(token))
                    {
                        var buf = new ByteBuffer(token.Data.Length + 16);
                        buf.Append("unknown flag: "u8);
                        buf.Append(token.Data);
                        ConsoleOut.Error(buf.Span);
                        result = new ParsedArgs(startDir, force, Array.Empty<Arg>());
                        return false;
                    }
                    else
                    {
                        positional.Add(token);
                    }
                    break;

                case State.ExpectingProjectPath:
                    startDir = Fs.GetFullPath(token.AsFast);
                    state = State.Normal;
                    break;
            }
        }

        if (state == State.ExpectingProjectPath)
        {
            ConsoleOut.Error("-p requires a path argument"u8);
            result = new ParsedArgs(startDir, force, Array.Empty<Arg>());
            return false;
        }

        result = new ParsedArgs(startDir, force, positional.ToArray());
        return true;
    }

    private static bool IsHelpToken(Arg t)
        => t.AsFast == "--help"u8 || t.AsFast == "-h"u8;

    private static bool IsFlagToken(Arg t)
        => t.Data.Length > 0 && t.Data[0] == (byte)'-';
}
