using System.Text;
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
        catch (ArgumentException ex)
        {
            // TEMP(Phase3): ex.Message is a string; Phase 3 will propagate byte errors.
            ConsoleOut.Error(Encoding.UTF8.GetBytes(ex.Message));
            return 1;
        }

        var rest = parsed.Positional;
        var startDir = parsed.StartDir;

        if (rest.Length == 0)
        {
            ConsoleOut.Info(Help_u8);
            return 1;
        }
        if (rest[0] == "--help")
        {
            ConsoleOut.Info(Help_u8);
            return 0;
        }

        var arg = rest[0];
        if (!IsValidOwnerRepo(arg))
        {
            var buf = new ByteBuffer(64);
            buf.Append("invalid argument: '"u8);
            // TEMP(Phase3): arg is string; Phase 3 will propagate byte args.
            buf.Append(Encoding.UTF8.GetBytes(arg));
            buf.Append("' (expected <owner>/<repo>)"u8);
            ConsoleOut.Error(buf.Span);
            return 1;
        }

        var repoRoot = RepoRootResolver.Find(startDir);
        if (repoRoot == null)
        {
            ConsoleOut.Error("dotai requires a git repository"u8);
            return 1;
        }

        if (Directory.Exists(Path.Combine(repoRoot, ".skillshare")))
            ConsoleOut.Warn(".skillshare present. Please uninstall or reconfigure."u8);

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
                var buf = new ByteBuffer(128);
                buf.Append("config at "u8);
                // TEMP(Phase3): configPath is string; Phase 3 will propagate byte paths.
                buf.Append(Encoding.UTF8.GetBytes(configPath));
                buf.Append(" is malformed. Fix the file, or rerun with --force to reset (all previous configuration will be lost)."u8);
                ConsoleOut.Error(buf.Span);
                return 2;
            }
            config = new List<string>();
            ConfigStore.Save(configPath, config);
            ConsoleOut.Warn("--force: reset malformed config. previous configuration lost."u8);
        }
        var alreadyRegistered = config.Contains(url);
        if (alreadyRegistered)
        {
            var buf = new ByteBuffer(64);
            buf.Append("repository already registered: "u8);
            // TEMP(Phase3): url is string; Phase 3 will propagate byte URLs.
            buf.Append(Encoding.UTF8.GetBytes(url));
            ConsoleOut.Hint(buf.Span);
        }
        ConfigStore.AddRepo(config, url);
        ConfigStore.Save(configPath, config);

        var cloneDir = Path.Combine(reposDir, cloneName);
        if (!Directory.Exists(Path.Combine(cloneDir, ".git")))
        {
            var result = GitClient.Clone(url, cloneDir);
            if (result.ExitCode != 0)
            {
                var stderrTrimmed = ByteOps.Trim(result.StdErr);
                var buf = new ByteBuffer(64);
                buf.Append("git clone failed: "u8);
                // TEMP(Phase3): stderr is already bytes — Trim returns span; decode only for message boundary.
                buf.Append(stderrTrimmed);
                ConsoleOut.Error(buf.Span);
                return 2;
            }
            var clonedBuf = new ByteBuffer(64);
            clonedBuf.Append("cloned "u8);
            // TEMP(Phase3): url is string; Phase 3 will propagate byte URLs.
            clonedBuf.Append(Encoding.UTF8.GetBytes(url));
            ConsoleOut.Info(clonedBuf.Span);
        }

        Robot.ShowIfTty();

        var sync = new SyncCommand(startDir) { Silent = true, Force = parsed.Force };
        var syncCode = sync.Execute(Array.Empty<string>());
        if (syncCode == 0)
        {
            var report = sync.LastReport;
            var skills = report?.SkillsLinked ?? 0;
            var files = report?.FilesLinked ?? 0;
            var buf = new ByteBuffer(128);
            buf.Append("registered "u8);
            // TEMP(Phase3): url is string; Phase 3 will propagate byte URLs.
            buf.Append(Encoding.UTF8.GetBytes(url));
            buf.Append(": "u8);
            buf.AppendInt(skills);
            buf.Append(" skills, "u8);
            buf.AppendInt(files);
            buf.Append(" files synced"u8);
            ConsoleOut.Success(buf.Span);
        }
        return syncCode;
    }

    private static readonly byte[] Help_u8 =
        "dotai init [standard flags] <owner>/<repo> — register a source repository and sync."u8.ToArray();

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
