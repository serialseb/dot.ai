using Dotai.Commands;
using Dotai.Ui;

namespace Dotai;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0) return new HelpCommand().Execute(args);

        var first = args[0];
        var rest = args[1..];

        if (first == "--help" || first == "-h") return new HelpCommand().Execute(rest);
        if (first == "init") return new InitCommand().Execute(rest);
        if (first == "sync") return new SyncCommand().Execute(rest);

        ConsoleOut.Error($"unknown command: {first}");
        new HelpCommand().Execute(Array.Empty<string>());
        return 1;
    }
}
