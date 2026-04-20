using System.Text;
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

        var buf = new ByteBuffer(64);
        buf.Append("unknown command: "u8);
        // TEMP(Phase3): first is string argv; Phase 3 will use byte argv.
        buf.Append(Encoding.UTF8.GetBytes(first));
        ConsoleOut.Error(buf.Span);
        new HelpCommand().Execute(Array.Empty<string>());
        return 1;
    }
}
