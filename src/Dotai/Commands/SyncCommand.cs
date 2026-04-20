using Dotai.Native;
using Dotai.Services;
using Dotai.Ui;

namespace Dotai.Commands;

public sealed class SyncCommand
{
    private readonly NativeString _startDir;
    private SyncReport _lastReport;
    private bool _hasLastReport;

    public SyncCommand()
    {
        Fs.TryGetCurrentDirectory(out _startDir);
    }

    public SyncCommand(NativeStringView startDir)
    {
        _startDir = NativeString.From(startDir);
    }

    public bool Silent { get; init; }
    public bool Force { get; init; }

    public bool HasLastReport => _hasLastReport;
    public ref SyncReport LastReport => ref _lastReport;

    public int Execute(NativeListView<NativeString> args)
    {
        if (!SharedFlags.TryParse(args, _startDir.AsView(), out var parsed)) return 1;
        var force = parsed.Force || Force;
        int code = ExecuteCore(parsed.StartDir.AsView(), force, Silent, ref _lastReport);
        _hasLastReport = true;
        parsed.Dispose();
        return code;
    }

    // Static entry-point used by InitCommand (avoids creating a SyncCommand instance).
    public static int Execute(NativeStringView startDir, bool force, bool silent,
        out int skillsLinked, out int filesLinked)
    {
        var report = new SyncReport(4);
        int code = ExecuteCore(startDir, force, silent, ref report);
        skillsLinked = report.SkillsLinked;
        filesLinked = report.FilesLinked;
        report.Dispose();
        return code;
    }

    private static int ExecuteCore(NativeStringView startDir, bool force, bool silent, ref SyncReport report)
    {
        if (!RepoRootResolver.TryFind(startDir, out var repoRoot))
        {
            ConsoleOut.Error("dotai requires a git repository"u8);
            return 1;
        }

        var skillshareCheck = Fs.Combine(repoRoot.AsView(), ".skillshare"u8);
        if (Fs.IsDirectory(skillshareCheck.AsView()))
            ConsoleOut.Warn(".skillshare present. Please uninstall or reconfigure."u8);
        skillshareCheck.Dispose();

        var aiDir = Fs.Combine(repoRoot.AsView(), ".ai"u8);
        var configPath = Fs.Combine(aiDir.AsView(), "config.toml"u8);
        aiDir.Dispose();
        if (!ConfigStore.TryLoad(configPath.AsView(), out var config))
        {
            if (!force)
            {
                configPath.Dispose(); repoRoot.Dispose();
                return 2;
            }
            config = new NativeList<RepoConfig>(4);
            ConfigStore.Save(configPath.AsView(), config.AsView());
            ConsoleOut.Warn("--force: reset malformed config. previous configuration lost."u8);
        }
        configPath.Dispose();

        if (config.Length == 0)
        {
            for (int i = 0; i < config.Length; i++) config[i].Dispose();
            config.Dispose();
            repoRoot.Dispose();
            ConsoleOut.Error("no repositories configured (run dotai init first)"u8);
            return 1;
        }

        var agentNames = AgentDetector.Detect(repoRoot.AsView());

        if (force)
        {
            SkillLinker.ForceReset(repoRoot.AsView(), agentNames.AsView());
            ConsoleOut.Warn("--force: reset dotai-owned symlinks and config. previous dotai state lost."u8);
        }

        report = new SyncReport(4);

        for (int i = 0; i < config.Length; i++)
        {
            SyncOne(repoRoot.AsView(), config[i], agentNames.AsView(), silent, force, ref report);
        }

        int repoCount = config.Length;
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();

        if (!report.Ok)
        {
            ConsoleOut.Warn("completed with issues:"u8);
            for (int i = 0; i < report.ManualRepos.Length; i++)
            {
                var r = report.ManualRepos[i].AsView();
                var buf = new NativeBuffer(r.Length + 16);
                buf.Append("  \xe2\x80\xa2 manual: "u8);
                buf.Append(r);
                ConsoleOut.Detail(buf.AsView());
                buf.Dispose();
            }
            for (int i = 0; i < report.Conflicts.Length; i++)
            {
                var c = report.Conflicts[i].AsView();
                var buf = new NativeBuffer(c.Length + 18);
                buf.Append("  \xe2\x80\xa2 conflict: "u8);
                buf.Append(c);
                ConsoleOut.Detail(buf.AsView());
                buf.Dispose();
            }
            ConsoleOut.Hint("resolve the issues above, then run 'dotai sync' again"u8);
            for (int i = 0; i < agentNames.Length; i++) agentNames[i].Dispose();
            agentNames.Dispose();
            repoRoot.Dispose();
            return 3;
        }

        if (!silent)
        {
            NativeStringView plural = repoCount == 1 ? "repository"u8 : "repositories"u8;
            var buf = new NativeBuffer(64);
            buf.Append("synced "u8);
            buf.AppendInt(report.SkillsLinked);
            buf.Append(" skills, "u8);
            buf.AppendInt(report.FilesLinked);
            buf.Append(" files across "u8);
            buf.AppendInt(repoCount);
            buf.AppendByte((byte)' ');
            buf.Append(plural);
            ConsoleOut.Success(buf.AsView());
            buf.Dispose();
        }

        for (int i = 0; i < agentNames.Length; i++) agentNames[i].Dispose();
        agentNames.Dispose();
        repoRoot.Dispose();
        return 0;
    }

    private static ReadOnlySpan<byte> Help_u8 =>
        "dotai sync [standard flags] — sync all configured source repositories."u8;

    private static void SyncOne(NativeStringView repoRoot, in RepoConfig entry,
        NativeListView<NativeString> agents, bool silent, bool force, ref SyncReport report)
    {
        var nameView = entry.Name.AsView();

        // Build URL: https://github.com/<name>
        var urlBuf = new NativeBuffer(32 + nameView.Length);
        urlBuf.Append("https://github.com/"u8);
        urlBuf.Append(nameView);
        var urlNs = urlBuf.Freeze();

        // Clone dir: .ai/repositories/<owner>_<repo>
        var cloneName = GitClient.DeriveCloneName(nameView);
        var aiDir = Fs.Combine(repoRoot, ".ai"u8);
        var reposDir = Fs.Combine(aiDir.AsView(), "repositories"u8);
        aiDir.Dispose();
        var clone = Fs.Combine(reposDir.AsView(), cloneName.AsView());
        cloneName.Dispose(); reposDir.Dispose();

        var cloneDotGit = Fs.Combine(clone.AsView(), ".git"u8);
        if (!Fs.IsDirectory(cloneDotGit.AsView()))
        {
            cloneDotGit.Dispose();
            var msg = new NativeBuffer(clone.Length + 16);
            msg.Append(clone.AsView());
            msg.Append(" (not cloned)"u8);
            report.ManualRepos.Add(msg.Freeze());
            clone.Dispose(); urlNs.Dispose();
            return;
        }
        cloneDotGit.Dispose();

        if (GitClient.RebaseInProgress(clone.AsView()))
        {
            var msg = new NativeBuffer(clone.Length + 24);
            msg.Append(clone.AsView());
            msg.Append(" (rebase in progress)"u8);
            report.ManualRepos.Add(msg.Freeze());
            clone.Dispose(); urlNs.Dispose();
            return;
        }

        var status = GitClient.StatusPorcelain(clone.AsView());
        if (!status.StdOut.AsView().IsBlank())
        {
            if (!silent)
            {
                var prepBuf = new NativeBuffer(48 + nameView.Length);
                prepBuf.Append("preparing changes in "u8);
                prepBuf.Append(nameView);
                ConsoleOut.Step(prepBuf.AsView());
                prepBuf.Dispose();
            }
            var addResult = GitClient.AddAll(clone.AsView());
            addResult.Dispose();
            var commitResult = GitClient.Commit(clone.AsView(), "dotai sync"u8);
            if (addResult.ExitCode != 0 || commitResult.ExitCode != 0)
            {
                commitResult.Dispose();
                status.Dispose();
                var msg = new NativeBuffer(clone.Length + 20);
                msg.Append(clone.AsView());
                msg.Append(" (commit failed)"u8);
                report.ManualRepos.Add(msg.Freeze());
                clone.Dispose(); urlNs.Dispose();
                return;
            }
            commitResult.Dispose();
        }
        status.Dispose();

        if (!silent)
        {
            var syncBuf = new NativeBuffer(48 + nameView.Length);
            syncBuf.Append("syncing "u8);
            syncBuf.Append(nameView);
            syncBuf.Append(" with remote"u8);
            ConsoleOut.Step(syncBuf.AsView());
            syncBuf.Dispose();
        }
        var fetchResult = GitClient.Fetch(clone.AsView());
        if (fetchResult.ExitCode != 0)
        {
            fetchResult.Dispose();
            var msg = new NativeBuffer(clone.Length + 18);
            msg.Append(clone.AsView());
            msg.Append(" (fetch failed)"u8);
            report.ManualRepos.Add(msg.Freeze());
            clone.Dispose(); urlNs.Dispose();
            return;
        }
        fetchResult.Dispose();

        var branchNs = GitClient.DefaultBranch(clone.AsView());

        var upstreamBuf = new NativeBuffer(32);
        upstreamBuf.Append("origin/"u8);
        upstreamBuf.Append(branchNs.AsView());
        var upstream = upstreamBuf.Freeze();

        var rebase = GitClient.Rebase(clone.AsView(), upstream.AsView());
        upstream.Dispose();
        if (rebase.ExitCode != 0)
        {
            rebase.Dispose();
            branchNs.Dispose();
            var msg = new NativeBuffer(clone.Length + 48);
            msg.Append(clone.AsView());
            msg.Append(" (rebase failed; resolve in .git/rebase-merge)"u8);
            report.ManualRepos.Add(msg.Freeze());
            clone.Dispose(); urlNs.Dispose();
            return;
        }
        rebase.Dispose();

        if (!silent)
        {
            var sendBuf = new NativeBuffer(48 + nameView.Length);
            sendBuf.Append("sending changes to "u8);
            sendBuf.Append(nameView);
            ConsoleOut.Step(sendBuf.AsView());
            sendBuf.Dispose();
        }
        var push = GitClient.Push(clone.AsView(), branchNs.AsView());
        branchNs.Dispose();
        if (push.ExitCode != 0)
        {
            var stderrTrimmed = push.StdErr.AsView().Trim();
            var msg = new NativeBuffer(clone.Length + stderrTrimmed.Length + 20);
            msg.Append(clone.AsView());
            msg.Append(" (push failed: "u8);
            msg.Append(stderrTrimmed);
            msg.AppendByte((byte)')');
            push.Dispose();
            report.ManualRepos.Add(msg.Freeze());
            clone.Dispose(); urlNs.Dispose();
            return;
        }
        push.Dispose();

        var skillsBefore = report.SkillsLinked;
        var filesBefore = report.FilesLinked;
        SkillLinker.LinkSkills(repoRoot, clone.AsView(), agents, ref report, force);
        SkillLinker.LinkFiles(repoRoot, clone.AsView(), ref report, force);
        SkillLinker.CleanupOrphans(repoRoot, agents);
        var deltaSkills = report.SkillsLinked - skillsBefore;
        var deltaFiles = report.FilesLinked - filesBefore;

        if (!silent)
        {
            var msgBuf = new NativeBuffer(64);
            msgBuf.Append("  \xe2\x80\xa2 "u8);
            msgBuf.Append(nameView);
            msgBuf.Append(": "u8);
            msgBuf.AppendInt(deltaSkills);
            msgBuf.Append(" skills, "u8);
            msgBuf.AppendInt(deltaFiles);
            msgBuf.Append(" files"u8);
            ConsoleOut.Info(msgBuf.AsView());
            msgBuf.Dispose();
        }
        clone.Dispose();
        urlNs.Dispose();
    }
}
