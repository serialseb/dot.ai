namespace Dotai.Commands;

public sealed record ParsedArgs(string StartDir, bool Force, string[] Positional);

public static class SharedFlags
{
    public static ParsedArgs Parse(string[] args, string defaultStartDir)
    {
        var startDir = defaultStartDir;
        var force = false;
        var positional = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a == "-p" || a == "--project")
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException("-p requires a path argument");
                startDir = Path.GetFullPath(args[i + 1]);
                i++;
                continue;
            }
            if (a == "-f" || a == "--force")
            {
                force = true;
                continue;
            }
            if (a.StartsWith('-') && a != "--help" && a != "-h")
                throw new ArgumentException($"unknown flag: {a}");
            positional.Add(a);
        }

        return new ParsedArgs(startDir, force, positional.ToArray());
    }
}
