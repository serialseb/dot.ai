using Dotai.Services;
using Dotai.Text;
using Dotai.Ui;

namespace Dotai.Commands;

public sealed class InitCommand : ICommand
{
    private readonly byte[] _startDir;

    public InitCommand() : this(Fs.GetCurrentDirectory()) { }
    public InitCommand(byte[] startDir) { _startDir = startDir; }

    // TEST-SEAM: tests pass a string URL override; converted to bytes here.
    // This is the only remaining Encoding call in production-adjacent code.
    public byte[]? CloneUrlOverride { get; init; }

    public string Name => "init";
    public string Help => "dotai init [standard flags] <owner>/<repo> — register a source repository and sync.";

    public int Execute(Arg[] args)
    {
        if (!SharedFlags.TryParse(args, _startDir, out var parsed)) return 1;

        var rest = parsed.Positional;
        var startDir = parsed.StartDir;

        if (rest.Length == 0)
        {
            ConsoleOut.Info(Help_u8);
            return 1;
        }
        if (rest[0].AsFast.Equals((FastString)"--help"u8))
        {
            ConsoleOut.Info(Help_u8);
            return 0;
        }

        var arg = rest[0];
        if (!IsValidOwnerRepo(arg.Data))
        {
            var buf = new ByteBuffer(64);
            buf.Append("invalid argument: '"u8);
            buf.Append(arg.Data);
            buf.Append("' (expected <owner>/<repo>)"u8);
            ConsoleOut.Error(buf.Span);
            return 1;
        }

        if (!RepoRootResolver.TryFind(startDir, out var repoRoot))
        {
            ConsoleOut.Error("dotai requires a git repository"u8);
            return 1;
        }

        if (Fs.IsDirectory(Fs.Combine(repoRoot, ".skillshare"u8)))
            ConsoleOut.Warn(".skillshare present. Please uninstall or reconfigure."u8);

        // Split <owner>/<repo> on '/'
        int slash = arg.Data.AsSpan().IndexOf((byte)'/');
        var ownerBytes = arg.Data.AsSpan(0, slash).ToArray();
        var repoBytes  = arg.Data.AsSpan(slash + 1).ToArray();

        byte[] urlBytes;
        if (CloneUrlOverride != null)
        {
            urlBytes = CloneUrlOverride;
        }
        else
        {
            var urlBuf = new ByteBuffer(32 + ownerBytes.Length + repoBytes.Length);
            urlBuf.Append("https://github.com/"u8);
            urlBuf.Append(ownerBytes);
            urlBuf.AppendByte((byte)'/');
            urlBuf.Append(repoBytes);
            urlBytes = urlBuf.Span.ToArray();
        }

        FastString urlFast = urlBytes;
        var cloneNameBytes = GitClient.DeriveCloneName(urlFast);

        var aiDir = Fs.Combine(repoRoot, ".ai"u8);
        var reposDir = Fs.Combine(aiDir, "repositories"u8);
        Fs.CreateDirectory(reposDir);

        GitignoreWriter.EnsureLine(
            (FastString)Fs.Combine(aiDir, ".gitignore"u8),
            "repositories/"u8);

        var configPath = Fs.Combine(aiDir, "config.jsonc"u8);
        if (!ConfigStore.TryLoad((FastString)configPath, out var config))
        {
            if (!parsed.Force) return 2;
            config = new List<byte[]>();
            ConfigStore.Save((FastString)configPath, config);
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
        ConfigStore.Save((FastString)configPath, config);

        var cloneDir = Fs.Combine(reposDir, cloneNameBytes);
        if (!Fs.IsDirectory(Fs.Combine(cloneDir, ".git"u8)))
        {
            var result = GitClient.Clone(urlFast, (FastString)cloneDir);
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

        var sync = new SyncCommand(parsed.StartDir) { Silent = true, Force = parsed.Force };
        var syncCode = sync.Execute(Array.Empty<Arg>());
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

    private static bool IsValidOwnerRepo(byte[] data)
    {
        if (data.Length == 0) return false;
        int slashCount = 0;
        int segmentLength = 0;
        foreach (var b in data)
        {
            if (b == (byte)'/')
            {
                if (segmentLength == 0) return false;
                if (slashCount == 1) return false;
                slashCount++;
                segmentLength = 0;
                continue;
            }
            if (!IsAllowedByte(b)) return false;
            segmentLength++;
        }
        return slashCount == 1 && segmentLength > 0;
    }

    private static bool IsAllowedByte(byte b) =>
        (b >= (byte)'A' && b <= (byte)'Z')
        || (b >= (byte)'a' && b <= (byte)'z')
        || (b >= (byte)'0' && b <= (byte)'9')
        || b == (byte)'.' || b == (byte)'_' || b == (byte)'-';
}
