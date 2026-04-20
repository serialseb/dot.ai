using Dotai.Services;

namespace Dotai.Commands;

// STUB — Task 11 will replace the Execute body with real sync logic.
public sealed class SyncCommand : ICommand
{
    private readonly string _startDir;
    public SyncCommand() : this(Directory.GetCurrentDirectory()) { }
    public SyncCommand(string startDir) { _startDir = startDir; }
    public string Name => "sync";
    public string Help => "dotai sync — sync all configured source repositories.";
    public SyncReport? LastReport { get; private set; }
    public int Execute(string[] args) { LastReport = new SyncReport(); return 0; }
}
