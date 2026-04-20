using System.Text.RegularExpressions;
using Dotai.Services;
using Dotai.Ui;

namespace Dotai.Commands;

public sealed class InitCommand : ICommand
{
    private static readonly Regex ArgFormat = new(@"^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$");
    private readonly string _startDir;

    public InitCommand() : this(Directory.GetCurrentDirectory()) { }
    public InitCommand(string startDir) { _startDir = startDir; }

    public string? CloneUrlOverride { get; init; }

    public string Name => "init";
    public string Help => "dotai init [standard flags] <owner>/<repo> — register a source repository and sync.";

    public int Execute(string[] args)
    {
        ParsedArgs parsed;
        try { parsed = SharedFlags.Parse(args, _startDir); }
        catch (ArgumentException ex) { ConsoleOut.Error(ex.Message); return 1; }

        var rest = parsed.Positional;
        var startDir = parsed.StartDir;

        if (rest.Length == 0)
        {
            ConsoleOut.Info(Help);
            return 1;
        }
        if (rest[0] == "--help")
        {
            ConsoleOut.Info(Help);
            return 0;
        }

        var arg = rest[0];
        if (!ArgFormat.IsMatch(arg))
        {
            ConsoleOut.Error($"invalid argument: '{arg}' (expected <owner>/<repo>)");
            return 1;
        }

        var repoRoot = RepoRootResolver.Find(startDir);
        if (repoRoot == null)
        {
            ConsoleOut.Error("dotai requires a git repository");
            return 1;
        }

        var parts = arg.Split('/');
        var owner = parts[0];
        var repo = parts[1];
        var url = CloneUrlOverride ?? $"https://github.com/{owner}/{repo}";
        var cloneName = GitClient.DeriveCloneName(url);

        var aiDir = Path.Combine(repoRoot, ".ai");
        var reposDir = Path.Combine(aiDir, "repositories");
        Directory.CreateDirectory(reposDir);

        GitignoreWriter.EnsureLine(Path.Combine(aiDir, ".gitignore"), "repositories/");

        var configPath = Path.Combine(aiDir, "config.jsonc");
        var config = ConfigStore.Load(configPath);
        var alreadyRegistered = config.ContainsKey(url);
        if (alreadyRegistered) ConsoleOut.Hint($"repository already registered: {url}");
        ConfigStore.AddRepo(config, url);
        ConfigStore.Save(configPath, config);

        var cloneDir = Path.Combine(reposDir, cloneName);
        if (!Directory.Exists(Path.Combine(cloneDir, ".git")))
        {
            var result = GitClient.Clone(url, cloneDir);
            if (result.ExitCode != 0)
            {
                ConsoleOut.Error($"git clone failed: {result.StdErr.Trim()}");
                return 2;
            }
            ConsoleOut.Info($"cloned {url}");
        }

        Robot.ShowIfTty();

        var sync = new SyncCommand(startDir) { Silent = true };
        var syncCode = sync.Execute(Array.Empty<string>());
        if (syncCode == 0)
        {
            var report = sync.LastReport;
            var skills = report?.SkillsLinked ?? 0;
            var files = report?.FilesLinked ?? 0;
            ConsoleOut.Success($"registered {url}: {skills} skills, {files} files synced");
        }
        return syncCode;
    }
}
