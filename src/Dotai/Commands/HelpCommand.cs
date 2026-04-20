using Dotai.Ui;

namespace Dotai.Commands;

public sealed class HelpCommand : ICommand
{
    public string Name => "--help";
    public string Help => "Show this message.";

    public int Execute(string[] args)
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

            config is kept in .ai/config.jsonc at the git repo root.
            """u8);
        return 0;
    }
}
