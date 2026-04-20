using Dotai.Native;
using Dotai.Ui;

namespace Dotai.Commands;

public static class HelpCommand
{
    public static int Execute(NativeListView<NativeString> args)
    {
        ConsoleOut.Info("""
            dotai — share AI skills and files across projects via symlinks 🤖

            usage:
              dotai init <owner>/<repo>   register a source repository and sync
              dotai sync                  sync all configured repositories
              dotai --help                this message

            standard flags (accepted by every command, placed after the command):
              -p, --project <path>   run as if invoked from <path> (default: current directory)
              -f, --force            reset dotai-owned symlinks and overwrite malformed config

            config is kept in .ai/config.toml at the git repo root.
            """u8);
        return 0;
    }
}
