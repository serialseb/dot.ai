using System.Text;
using Dotai.Services;
using Dotai.Text;
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
            // TEMP(Phase3): ex.Message is a string; Phase 3c will propagate byte errors.
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
            // PHASE3-TEMP: arg is string from argv; Phase 3c will propagate byte args.
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

        // Compose the URL as bytes directly when possible; CloneUrlOverride is a TEMP string boundary.
        byte[] urlBytes;
        if (CloneUrlOverride != null)
        {
            // PHASE3-TEMP: CloneUrlOverride is a string override for tests; Phase 3c removes this path.
            urlBytes = Encoding.UTF8.GetBytes(CloneUrlOverride);
        }
        else
        {
            // Compose "https://github.com/<owner>/<repo>" via ByteBuffer — no string interpolation.
            // PHASE3-TEMP: owner/repo come from argv strings; Phase 3c will use byte args.
            var ownerBytes = Encoding.UTF8.GetBytes(owner);
            var repoBytes = Encoding.UTF8.GetBytes(repo);
            var urlBuf = new ByteBuffer(32 + ownerBytes.Length + repoBytes.Length);
            urlBuf.Append("https://github.com/"u8);
            urlBuf.Append(ownerBytes);
            urlBuf.AppendByte((byte)'/');
            urlBuf.Append(repoBytes);
            urlBytes = urlBuf.Span.ToArray();
        }

        FastString urlFast = urlBytes;
        var cloneNameBytes = GitClient.DeriveCloneName(urlFast);
        // PHASE3-TEMP: path APIs still take string; Phase 3b will use libc.
        var cloneName = Encoding.UTF8.GetString(cloneNameBytes);

        var aiDir = Path.Combine(repoRoot, ".ai");
        var reposDir = Path.Combine(aiDir, "repositories");
        Directory.CreateDirectory(reposDir);

        GitignoreWriter.EnsureLine(Path.Combine(aiDir, ".gitignore"), "repositories/");

        var configPath = Path.Combine(aiDir, "config.jsonc");
        List<byte[]> config;
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
                // PHASE3-TEMP: configPath is string; Phase 3b will propagate byte paths.
                buf.Append(Encoding.UTF8.GetBytes(configPath));
                buf.Append(" is malformed. Fix the file, or rerun with --force to reset (all previous configuration will be lost)."u8);
                ConsoleOut.Error(buf.Span);
                return 2;
            }
            config = new List<byte[]>();
            ConfigStore.Save(configPath, config);
            ConsoleOut.Warn("--force: reset malformed config. previous configuration lost."u8);
        }

        var alreadyRegistered = ContainsUrl(config, urlFast);
        if (alreadyRegistered)
        {
            var buf = new ByteBuffer(64);
            buf.Append("repository already registered: "u8);
            buf.Append(urlBytes);
            ConsoleOut.Hint(buf.Span);
        }
        ConfigStore.AddRepo(config, urlFast);
        ConfigStore.Save(configPath, config);

        var cloneDir = Path.Combine(reposDir, cloneName);
        if (!Directory.Exists(Path.Combine(cloneDir, ".git")))
        {
            // PHASE3-TEMP: GitClient.Clone still takes string URL; Phase 3b will flip.
            var result = GitClient.Clone(Encoding.UTF8.GetString(urlBytes), cloneDir);
            if (result.ExitCode != 0)
            {
                var stderrTrimmed = ByteOps.Trim(result.StdErr);
                var buf = new ByteBuffer(64);
                buf.Append("git clone failed: "u8);
                buf.Append(stderrTrimmed);
                ConsoleOut.Error(buf.Span);
                return 2;
            }
            var clonedBuf = new ByteBuffer(64);
            clonedBuf.Append("cloned "u8);
            clonedBuf.Append(urlBytes);
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
            buf.Append(urlBytes);
            buf.Append(": "u8);
            buf.AppendInt(skills);
            buf.Append(" skills, "u8);
            buf.AppendInt(files);
            buf.Append(" files synced"u8);
            ConsoleOut.Success(buf.Span);
        }
        return syncCode;
    }

    private static bool ContainsUrl(List<byte[]> config, FastString url)
    {
        foreach (var item in config)
            if (url.Equals(new FastString(item))) return true;
        return false;
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
