using Dotai.Native;
using Dotai.Services;
using Dotai.Ui;

namespace Dotai.Commands;

public struct ParsedArgs
{
    public NativeString StartDir;
    public bool Force;
    public NativeList<NativeString> Positional;

    public ParsedArgs(NativeString startDir, bool force, NativeList<NativeString> positional)
    {
        StartDir = startDir;
        Force = force;
        Positional = positional;
    }

    public void Dispose()
    {
        StartDir.Dispose();
        for (int i = 0; i < Positional.Length; i++) Positional[i].Dispose();
        Positional.Dispose();
    }
}

public static class SharedFlags
{
    private enum State
    {
        Normal,
        ExpectingProjectPath,
    }

    public static bool TryParse(NativeListView<NativeString> args, NativeStringView defaultStartDir, out ParsedArgs result)
    {
        var state = State.Normal;
        var startDir = NativeString.From(defaultStartDir);
        var force = false;
        var positional = new NativeList<NativeString>(args.Length > 0 ? args.Length : 4);

        for (int ti = 0; ti < args.Length; ti++)
        {
            var token = args[ti].AsView();
            switch (state)
            {
                case State.Normal:
                    if (token == "-p"u8 || token == "--project"u8)
                    {
                        state = State.ExpectingProjectPath;
                    }
                    else if (token == "-f"u8 || token == "--force"u8)
                    {
                        force = true;
                    }
                    else if (IsHelpToken(token))
                    {
                        positional.Add(NativeString.From(token));
                    }
                    else if (IsFlagToken(token))
                    {
                        var buf = new NativeBuffer(token.Length + 16);
                        buf.Append("unknown flag: "u8);
                        buf.Append(token);
                        ConsoleOut.Error(buf.AsView());
                        buf.Dispose();
                        for (int i = 0; i < positional.Length; i++) positional[i].Dispose();
                        positional.Dispose();
                        startDir.Dispose();
                        result = default;
                        return false;
                    }
                    else
                    {
                        positional.Add(NativeString.From(token));
                    }
                    break;

                case State.ExpectingProjectPath:
                    startDir.Dispose();
                    startDir = Fs.GetFullPath(token);
                    state = State.Normal;
                    break;
            }
        }

        if (state == State.ExpectingProjectPath)
        {
            ConsoleOut.Error("-p requires a path argument"u8);
            for (int i = 0; i < positional.Length; i++) positional[i].Dispose();
            positional.Dispose();
            startDir.Dispose();
            result = default;
            return false;
        }

        result = new ParsedArgs(startDir, force, positional);
        return true;
    }

    private static bool IsHelpToken(NativeStringView t)
        => t == "--help"u8 || t == "-h"u8;

    private static bool IsFlagToken(NativeStringView t)
        => t.Length > 0 && t[0] == (byte)'-';
}
