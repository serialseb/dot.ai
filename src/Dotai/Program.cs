using System.Runtime.InteropServices;
using Dotai.Commands;
using Dotai.Text;
using Dotai.Ui;

namespace Dotai;

public static unsafe class Program
{
    // Managed entry point stub — only invoked in non-AOT (test/debug) builds.
    // In AOT, ILC replaces this with NativeMain via CustomNativeMain + UnmanagedCallersOnly.
    public static int Main() => 0;

    [UnmanagedCallersOnly(EntryPoint = "main")]
    public static int NativeMain(int argc, byte** argv)
    {
        // argv is a C-style array of null-terminated UTF-8 byte*.
        // Skip argv[0] (program name). No string is ever materialised.
        var count = argc > 1 ? argc - 1 : 0;
        var args = new Arg[count];
        for (int i = 0; i < count; i++)
            args[i] = new Arg(FastString.CloneNullTerminated(argv[i + 1]));

        if (args.Length == 0) return new HelpCommand().Execute(args);

        var first = args[0].AsFast;
        var rest  = args[1..];

        if (first.Equals("--help"u8) || first.Equals("-h"u8))
            return new HelpCommand().Execute(rest);
        if (first.Equals("init"u8))
            return new InitCommand().Execute(rest);
        if (first.Equals("sync"u8))
            return new SyncCommand().Execute(rest);

        var buf = new ByteBuffer(32);
        buf.Append("unknown command: "u8);
        buf.Append(first.Bytes);
        ConsoleOut.Error(buf.Span);
        new HelpCommand().Execute(Array.Empty<Arg>());
        return 1;
    }
}
