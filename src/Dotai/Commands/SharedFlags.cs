using Dotai.Services;
using Dotai.Text;

namespace Dotai.Commands;

public sealed record ParsedArgs(byte[] StartDir, bool Force, Arg[] Positional);

public static class SharedFlags
{
    private enum State
    {
        Normal,
        ExpectingProjectPath,
    }

    public static ParsedArgs Parse(Arg[] args, FastString defaultStartDir)
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
                    if (token.AsFast.Equals((FastString)"-p"u8) || token.AsFast.Equals((FastString)"--project"u8))
                    {
                        state = State.ExpectingProjectPath;
                    }
                    else if (token.AsFast.Equals((FastString)"-f"u8) || token.AsFast.Equals((FastString)"--force"u8))
                    {
                        force = true;
                    }
                    else if (IsHelpToken(token))
                    {
                        positional.Add(token);
                    }
                    else if (IsFlagToken(token))
                    {
                        var buf = new Ui.ByteBuffer(token.Data.Length + 16);
                        buf.Append("unknown flag: "u8);
                        buf.Append(token.Data);
                        throw new System.ArgumentException(System.Text.Encoding.UTF8.GetString(buf.Span));
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
            throw new System.ArgumentException("-p requires a path argument");

        return new ParsedArgs(startDir, force, positional.ToArray());
    }

    private static bool IsHelpToken(Arg t)
        => t.AsFast.Equals((FastString)"--help"u8) || t.AsFast.Equals((FastString)"-h"u8);

    private static bool IsFlagToken(Arg t)
        => t.Data.Length > 0 && t.Data[0] == (byte)'-';
}
