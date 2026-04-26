using System.Runtime.InteropServices;
using Dotai.Commands;
using Dotai.Native;
using Dotai.Ui;

namespace Dotai;

public static unsafe class Program
{
    // Managed entry point stub — only invoked in non-AOT (test/debug) builds.
    public static int Main() => 0;

    [UnmanagedCallersOnly(EntryPoint = "main")]
    public static int NativeMain(int argc, byte** argv)
    {
        var args = new NativeList<NativeString>(argc > 1 ? argc - 1 : 0);
        for (int i = 1; i < argc; i++)
            args.Add(NativeString.FromNullTerminated(argv[i]));

        int code = Dispatch(args.AsView());

        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();
        return code;
    }

    private static int Dispatch(NativeListView<NativeString> args)
    {
        if (args.Length == 0) return new StatusCommand().Execute(args);
        var first = args[0].AsView();
        var rest = args.Slice(1);
        if (first == "--help"u8 || first == "-h"u8) return HelpCommand.Execute(rest);
        if (first == "init"u8) return new InitCommand().Execute(rest);
        if (first == "sync"u8) return new SyncCommand().Execute(rest);

        var buf = new NativeBuffer(64);
        buf.Append("unknown command: "u8);
        buf.Append(first);
        ConsoleOut.Error(buf.AsView());
        buf.Dispose();
        HelpCommand.Execute(args);
        return 1;
    }
}
