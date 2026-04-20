using System.Text;
using Dotai.Commands;
using Dotai.Text;
using Dotai.Ui;

namespace Dotai;

public static class Program
{
    public static int Main(string[] args)
    {
        // CLR hands us string[] at this single entry point.
        // Convert once to Arg[] (UTF-8 bytes) and dispatch bytes from here on.
        var argv = new Arg[args.Length];
        for (int i = 0; i < args.Length; i++)
            argv[i] = new Arg(Encoding.UTF8.GetBytes(args[i]));

        if (argv.Length == 0) return new HelpCommand().Execute(argv);

        var first = argv[0];
        var rest  = argv[1..];

        if (first.AsFast.Equals((FastString)"--help"u8) || first.AsFast.Equals((FastString)"-h"u8))
            return new HelpCommand().Execute(rest);
        if (first.AsFast.Equals((FastString)"init"u8))
            return new InitCommand().Execute(rest);
        if (first.AsFast.Equals((FastString)"sync"u8))
            return new SyncCommand().Execute(rest);

        var buf = new ByteBuffer(64);
        buf.Append("unknown command: "u8);
        buf.Append(first.Data);
        ConsoleOut.Error(buf.Span);
        new HelpCommand().Execute(Array.Empty<Arg>());
        return 1;
    }
}
