using Dotai.Services;
using Dotai.Ui;

namespace Dotai.Commands;

public sealed class InitCommand : ICommand
{
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
        if (!IsValidOwnerRepo(arg))
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

        if (Directory.Exists(Path.Combine(repoRoot, ".skillshare")))
            ConsoleOut.Warn(".skillshare present. Please uninstall or reconfigure.");

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
        List<string> config;
        try
        {
            config = ConfigStore.Load(configPath);
        }
        catch (InvalidDataException)
        {
            if (!parsed.Force)
            {
                ConsoleOut.Error($"config at {configPath} is malformed. Fix the file, or rerun with --force to reset (all previous configuration will be lost).");
                return 2;
            }
            config = new List<string>();
            ConfigStore.Save(configPath, config);
            ConsoleOut.Warn("--force: reset malformed config. previous configuration lost.");
        }
        var alreadyRegistered = config.Contains(url);
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

        var sync = new SyncCommand(startDir) { Silent = true, Force = parsed.Force };
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

    private static bool IsValidOwnerRepo(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        int slashCount = 0;
        int segmentLength = 0;
        foreach (var c in s)
        {
            if (c == '/')
            {
                if (segmentLength == 0) return false; // empty owner or adjacent slashes
                if (slashCount == 1) return false;    // more than one slash
                slashCount++;
                segmentLength = 0;
                continue;
            }
            if (!IsAllowedChar(c)) return false;
            segmentLength++;
        }
        return slashCount == 1 && segmentLength > 0;
    }

    private static bool IsAllowedChar(char c) =>
        (c >= 'A' && c <= 'Z')
        || (c >= 'a' && c <= 'z')
        || (c >= '0' && c <= '9')
        || c == '.' || c == '_' || c == '-';
}
