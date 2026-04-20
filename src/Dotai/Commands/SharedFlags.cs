using System.Collections.Generic;
using System.Text;
using Dotai.Services;

namespace Dotai.Commands;

public sealed record ParsedArgs(string StartDir, bool Force, string[] Positional);

public static class SharedFlags
{
    private enum State
    {
        Normal,
        ExpectingProjectPath,
    }

    public static ParsedArgs Parse(string[] args, string defaultStartDir)
    {
        var state = State.Normal;
        var startDir = defaultStartDir;
        var force = false;
        var positional = new List<string>();

        foreach (var token in args)
        {
            switch (state)
            {
                case State.Normal:
                    if (token == "-p" || token == "--project")
                    {
                        state = State.ExpectingProjectPath;
                    }
                    else if (token == "-f" || token == "--force")
                    {
                        force = true;
                    }
                    else if (IsHelpToken(token))
                    {
                        positional.Add(token);
                    }
                    else if (IsFlagToken(token))
                    {
                        throw new System.ArgumentException($"unknown flag: {token}");
                    }
                    else
                    {
                        positional.Add(token);
                    }
                    break;

                case State.ExpectingProjectPath:
                    startDir = Encoding.UTF8.GetString(Fs.GetFullPath(Encoding.UTF8.GetBytes(token)));
                    state = State.Normal;
                    break;
            }
        }

        if (state == State.ExpectingProjectPath)
            throw new System.ArgumentException("-p requires a path argument");

        return new ParsedArgs(startDir, force, positional.ToArray());
    }

    private static bool IsHelpToken(string t) => t == "--help" || t == "-h";

    private static bool IsFlagToken(string t) => t.Length > 0 && t[0] == '-';
}
