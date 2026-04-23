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
        if (!IsValidRepoSpec(arg))
        {
            var buf = new NativeBuffer(96);
            buf.Append("invalid argument: '"u8);
            buf.Append(arg);
            buf.Append("' (expected <owner>/<repo> or <host>/<owner>/<repo>)"u8);
            ConsoleOut.Error(buf.AsView());
            buf.Dispose();
            parsed.Dispose();
            return 1;
        }

        ConsoleOut.Step("finding repository"u8);
        if (!RepoRootResolver.TryFind(startDir, out var repoRoot))
        {
            ConsoleOut.Error("dotai requires a git repository"u8);
            parsed.Dispose();
            return 1;
        }

        if (SkillshareMigrator.IsProjectMode(repoRoot.AsView()))
        {
            if (!PromptForSkillshareMigration())
            {
                repoRoot.Dispose();
                parsed.Dispose();
                return 1;
            }
            if (!SkillshareMigrator.TryMigrate(repoRoot.AsView(), out var stats))
            {
                repoRoot.Dispose();
                parsed.Dispose();
                return 2;
            }
            EmitMigrationSummary(stats);
        }

        // Build clone URL from repo spec (owner/repo or host[:port]/owner/repo).
        NativeString urlNs;
        if (!CloneUrlOverride.IsEmpty)
            urlNs = NativeString.From(CloneUrlOverride.AsView());
        else
            urlNs = GitClient.BuildCloneUrl(arg);

        // Derive clone dir name from owner/repo (replaces '/' with '_')
        var cloneName = GitClient.DeriveCloneName(arg);

        var aiDir = Fs.Combine(repoRoot.AsView(), ".ai"u8);
        var reposDir = Fs.Combine(aiDir.AsView(), "repositories"u8);
        Fs.TryCreateDirectory(reposDir.AsView());

        var gitignorePath = Fs.Combine(aiDir.AsView(), ".gitignore"u8);
        GitignoreWriter.EnsureLine(gitignorePath.AsView(), "repositories/"u8);
        gitignorePath.Dispose();

        var configPath = Fs.Combine(aiDir.AsView(), "config.toml"u8);
        if (!ConfigStore.TryLoad(configPath.AsView(), out var config))
        {
            if (!parsed.Force)
            {
                urlNs.Dispose(); cloneName.Dispose(); aiDir.Dispose(); reposDir.Dispose();
                configPath.Dispose(); repoRoot.Dispose(); parsed.Dispose();
                return 2;
            }
            config = new NativeList<RepoConfig>(4);
            ConfigStore.Save(configPath.AsView(), config.AsView());
            ConsoleOut.Warn("--force: reset malformed config. previous configuration lost."u8);
        }

        var alreadyRegistered = ConfigStore.Contains(config.AsView(), arg);
        if (alreadyRegistered)
        {
            var buf = new NativeBuffer(64);
            buf.Append("repository already registered: "u8);
            buf.Append(arg);
            ConsoleOut.Hint(buf.AsView());
            buf.Dispose();
        }
        ConfigStore.AddRepo(ref config, arg, "merge"u8);
        ConfigStore.Save(configPath.AsView(), config.AsView());
        configPath.Dispose();
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();

        var cloneDir = Fs.Combine(reposDir.AsView(), cloneName.AsView());
        cloneName.Dispose(); reposDir.Dispose(); aiDir.Dispose();

        var cloneDotGit = Fs.Combine(cloneDir.AsView(), ".git"u8);
        if (!Fs.IsDirectory(cloneDotGit.AsView()))
        {
            cloneDotGit.Dispose();
            var downloadBuf = new NativeBuffer(32 + arg.Length);
            downloadBuf.Append("downloading "u8);
            downloadBuf.Append(arg);
            ConsoleOut.Step(downloadBuf.AsView());
            downloadBuf.Dispose();
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
            result.Dispose();
        }
        else
        {
            cloneDotGit.Dispose();
        }

        urlNs.Dispose();

        Robot.ShowIfTty();

        ConsoleOut.Step("installing skills and files"u8);
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

    private static bool PromptForSkillshareMigration()
    {
        ConsoleOut.Info("Skillshare is incompatible with dotai."u8);
        ConsoleOut.Info("If you wish, you can try dotai and migrate your existing configurtion."u8);
        ConsoleOut.Info("If you want to go back at any point, just use `dotai init --uninstall`"u8);
        ConsoleOut.Info("and your skillshare configuraiton will be restored."u8);
        ConsoleOut.WriteLineStdout(""u8);
        ConsoleOut.WriteLineStdout("Install dotai? (y/n): "u8);
        return ConsoleIn.ReadYesNo();
    }

    private static void EmitMigrationSummary(MigrationStats stats)
    {
        var buf = new NativeBuffer(80);
        buf.Append("migrated "u8);
        buf.AppendInt(stats.Skills);
        buf.Append(" skills, "u8);
        buf.AppendInt(stats.Files);
        buf.Append(" files from Skillshare"u8);
        ConsoleOut.Success(buf.AsView());
        buf.Dispose();
    }

    private static ReadOnlySpan<byte> Help_u8 =>
        "dotai init [standard flags] <owner>/<repo> | <host>[:port]/<owner>/<repo> — register a source repository and sync."u8;

    // Accepts:
    //   owner/repo                      (GitHub shortcut)
    //   host/owner/repo                 (any host, verbatim)
    //   host:port/owner/repo            (host with port)
    // Host segment is not validated for character content — IDN and
    // multilingual hosts are allowed. Owner and repo segments keep the
    // existing restrictive char class.
    private static bool IsValidRepoSpec(NativeStringView data)
    {
        if (data.Length == 0) return false;

        int firstSlash = data.Bytes.IndexOf((byte)'/');
        int lastSlash = data.Bytes.LastIndexOf((byte)'/');
        if (firstSlash <= 0 || firstSlash == data.Length - 1) return false;

        if (firstSlash == lastSlash)
            return IsValidPathSegment(data.Bytes[..firstSlash])
                && IsValidPathSegment(data.Bytes[(firstSlash + 1)..]);

        int midStart = firstSlash + 1;
        int midEnd = lastSlash;
        if (midEnd <= midStart) return false;
        var mid = data.Bytes[midStart..midEnd];
        if (mid.IndexOf((byte)'/') >= 0) return false;

        return IsValidPathSegment(mid)
            && IsValidPathSegment(data.Bytes[(lastSlash + 1)..]);
    }

    private static bool IsValidPathSegment(ReadOnlySpan<byte> seg)
    {
        if (seg.IsEmpty) return false;
        for (int i = 0; i < seg.Length; i++)
            if (!IsAllowedByte(seg[i])) return false;
        return true;
    }

    private static bool IsAllowedByte(byte b) =>
        (b >= (byte)'A' && b <= (byte)'Z')
        || (b >= (byte)'a' && b <= (byte)'z')
        || (b >= (byte)'0' && b <= (byte)'9')
        || b == (byte)'.' || b == (byte)'_' || b == (byte)'-';
}
