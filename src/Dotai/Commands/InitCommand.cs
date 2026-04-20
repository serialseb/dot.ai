using Dotai.Native;
using Dotai.Services;
using Dotai.Ui;

namespace Dotai.Commands;

public sealed class InitCommand
{
    private readonly NativeString _startDir;

    public InitCommand()
    {
        Fs.TryGetCurrentDirectory(out _startDir);
    }

    public InitCommand(NativeStringView startDir)
    {
        _startDir = NativeString.From(startDir);
    }

    // TEST-SEAM: tests pass a pre-encoded URL override (as NativeString — caller owns, we copy).
    public NativeString CloneUrlOverride { get; init; }

    public int Execute(NativeListView<NativeString> args)
    {
        if (!SharedFlags.TryParse(args, _startDir.AsView(), out var parsed)) return 1;

        var positional = parsed.Positional;
        var startDir = parsed.StartDir.AsView();

        if (positional.Length == 0)
        {
            ConsoleOut.Info(Help_u8);
            parsed.Dispose();
            return 1;
        }
        if (positional[0].AsView() == "--help"u8)
        {
            ConsoleOut.Info(Help_u8);
            parsed.Dispose();
            return 0;
        }

        var arg = positional[0].AsView();
        if (!IsValidOwnerRepo(arg))
        {
            var buf = new NativeBuffer(64);
            buf.Append("invalid argument: '"u8);
            buf.Append(arg);
            buf.Append("' (expected <owner>/<repo>)"u8);
            ConsoleOut.Error(buf.AsView());
            buf.Dispose();
            parsed.Dispose();
            return 1;
        }

        if (!RepoRootResolver.TryFind(startDir, out var repoRoot))
        {
            ConsoleOut.Error("dotai requires a git repository"u8);
            parsed.Dispose();
            return 1;
        }

        var skillshareCheck = Fs.Combine(repoRoot.AsView(), ".skillshare"u8);
        if (Fs.IsDirectory(skillshareCheck.AsView()))
            ConsoleOut.Warn(".skillshare present. Please uninstall or reconfigure."u8);
        skillshareCheck.Dispose();

        // Split <owner>/<repo> on '/'
        int slash = arg.IndexOf((byte)'/');
        var ownerBytes = arg.Slice(0, slash);
        var repoBytes = arg.Slice(slash + 1);

        NativeString urlNs;
        if (!CloneUrlOverride.IsEmpty)
        {
            urlNs = NativeString.From(CloneUrlOverride.AsView());
        }
        else
        {
            var urlBuf = new NativeBuffer(32 + ownerBytes.Length + repoBytes.Length);
            urlBuf.Append("https://github.com/"u8);
            urlBuf.Append(ownerBytes);
            urlBuf.AppendByte((byte)'/');
            urlBuf.Append(repoBytes);
            urlNs = urlBuf.Freeze();
        }

        var cloneName = GitClient.DeriveCloneName(urlNs.AsView());

        var aiDir = Fs.Combine(repoRoot.AsView(), ".ai"u8);
        var reposDir = Fs.Combine(aiDir.AsView(), "repositories"u8);
        Fs.TryCreateDirectory(reposDir.AsView());

        var gitignorePath = Fs.Combine(aiDir.AsView(), ".gitignore"u8);
        GitignoreWriter.EnsureLine(gitignorePath.AsView(), "repositories/"u8);
        gitignorePath.Dispose();

        var configPath = Fs.Combine(aiDir.AsView(), "config.jsonc"u8);
        if (!ConfigStore.TryLoad(configPath.AsView(), out var config))
        {
            if (!parsed.Force)
            {
                urlNs.Dispose(); cloneName.Dispose(); aiDir.Dispose(); reposDir.Dispose();
                configPath.Dispose(); repoRoot.Dispose(); parsed.Dispose();
                return 2;
            }
            config = new NativeList<NativeString>(4);
            ConfigStore.Save(configPath.AsView(), config);
            ConsoleOut.Warn("--force: reset malformed config. previous configuration lost."u8);
        }

        var alreadyRegistered = ContainsUrl(config, urlNs.AsView());
        if (alreadyRegistered)
        {
            var buf = new NativeBuffer(64);
            buf.Append("repository already registered: "u8);
            buf.Append(urlNs.AsView());
            ConsoleOut.Hint(buf.AsView());
            buf.Dispose();
        }
        ConfigStore.AddRepo(ref config, urlNs.AsView());
        ConfigStore.Save(configPath.AsView(), config);
        configPath.Dispose();
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();

        var cloneDir = Fs.Combine(reposDir.AsView(), cloneName.AsView());
        cloneName.Dispose(); reposDir.Dispose(); aiDir.Dispose();

        var cloneDotGit = Fs.Combine(cloneDir.AsView(), ".git"u8);
        if (!Fs.IsDirectory(cloneDotGit.AsView()))
        {
            cloneDotGit.Dispose();
            var result = GitClient.Clone(urlNs.AsView(), cloneDir.AsView());
            if (result.ExitCode != 0)
            {
                var stderrTrimmed = result.StdErr.AsView().Trim();
                var buf = new NativeBuffer(64);
                buf.Append("git clone failed: "u8);
                buf.Append(stderrTrimmed);
                ConsoleOut.Error(buf.AsView());
                buf.Dispose();
                result.Dispose();
                urlNs.Dispose(); cloneDir.Dispose(); repoRoot.Dispose(); parsed.Dispose();
                return 2;
            }
            var clonedBuf = new NativeBuffer(64);
            clonedBuf.Append("cloned "u8);
            clonedBuf.Append(urlNs.AsView());
            ConsoleOut.Info(clonedBuf.AsView());
            clonedBuf.Dispose();
            result.Dispose();
        }
        else
        {
            cloneDotGit.Dispose();
        }

        urlNs.Dispose();

        Robot.ShowIfTty();

        var syncCode = SyncCommand.Execute(parsed.StartDir.AsView(), parsed.Force, silent: true,
            out int skills, out int files);
        if (syncCode == 0)
        {
            var buf = new NativeBuffer(128);
            buf.Append("registered "u8);
            buf.Append(cloneDir.AsView());
            buf.Append(": "u8);
            buf.AppendInt(skills);
            buf.Append(" skills, "u8);
            buf.AppendInt(files);
            buf.Append(" files synced"u8);
            ConsoleOut.Success(buf.AsView());
            buf.Dispose();
        }
        cloneDir.Dispose();
        repoRoot.Dispose();
        parsed.Dispose();
        return syncCode;
    }

    private static bool ContainsUrl(NativeList<NativeString> config, NativeStringView url)
    {
        for (int i = 0; i < config.Length; i++)
            if (config[i].AsView() == url) return true;
        return false;
    }

    private static ReadOnlySpan<byte> Help_u8 =>
        "dotai init [standard flags] <owner>/<repo> — register a source repository and sync."u8;

    private static bool IsValidOwnerRepo(NativeStringView data)
    {
        if (data.Length == 0) return false;
        int slashCount = 0;
        int segmentLength = 0;
        for (int i = 0; i < data.Length; i++)
        {
            var b = data[i];
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
