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
        long startMs = Environment.TickCount64;

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

        // Config.toml is the source of truth: drop any clone directory under
        // .ai/repositories/ whose name does not appear in the active config,
        // unless it was produced by a Skillshare migration (those stay put
        // so dotai init --uninstall can rehydrate them).
        ReconcileRepositories(repoRoot.AsView(), config.AsView());

        report = new SyncReport(4);

        if (!silent)
        {
            ConsoleOut.WriteLineStdout("🔄 Syncing repositories…"u8);
            ConsoleOut.WriteLineStdout(""u8);
        }

        for (int i = 0; i < config.Length; i++)
        {
            SyncOne(repoRoot.AsView(), config[i], agentNames.AsView(), silent, force, ref report);
            if (!silent) ConsoleOut.WriteLineStdout(""u8);
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
            EmitCategoryLine(
                "🧠 Skills"u8,
                report.SkillsNew, report.SkillsUpdated, report.SkillsGone);
            EmitCategoryLine(
                "📁 Files "u8,
                report.FilesNew, report.FilesUpdated, report.FilesGone);

            long elapsedMs = Environment.TickCount64 - startMs;
            EmitElapsed(elapsedMs);
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

        // Build URL from spec (owner/repo or host[:port]/owner/repo).
        var urlNs = GitClient.BuildCloneUrl(nameView);

        // Short name = last path segment of nameView
        var shortName = nameView;
        int slash = nameView.Bytes.LastIndexOf((byte)'/');
        if (slash >= 0) shortName = new NativeStringView(nameView.Bytes[(slash + 1)..]);

        // Clone dir: .ai/repositories/<host>▸<owner>▸<repo>
        var cloneName = GitClient.DeriveCloneName(nameView);
        var legacyName = GitClient.DeriveLegacyCloneName(nameView);
        var aiDir = Fs.Combine(repoRoot, ".ai"u8);
        var reposDir = Fs.Combine(aiDir.AsView(), "repositories"u8);
        aiDir.Dispose();
        var clone = Fs.Combine(reposDir.AsView(), cloneName.AsView());

        // One-shot rename of a pre-existing '_'-joined legacy clone dir so
        // setups created before the triangle-separator scheme keep working
        // without forcing a re-clone.
        if (!cloneName.AsView().Bytes.SequenceEqual(legacyName.AsView().Bytes))
        {
            var legacyClone = Fs.Combine(reposDir.AsView(), legacyName.AsView());
            var legacyDotGit = Fs.Combine(legacyClone.AsView(), ".git"u8);
            if (!Fs.IsDirectory(clone.AsView()) && Fs.IsDirectory(legacyDotGit.AsView()))
                Fs.TryRename(legacyClone.AsView(), clone.AsView());
            legacyDotGit.Dispose();
            legacyClone.Dispose();
        }
        cloneName.Dispose(); legacyName.Dispose(); reposDir.Dispose();

        if (!silent) EmitRepoHeader(shortName, urlNs.AsView());

        var cloneDotGit = Fs.Combine(clone.AsView(), ".git"u8);
        if (!Fs.IsDirectory(cloneDotGit.AsView()))
        {
            cloneDotGit.Dispose();
            if (!silent) ConsoleOut.WriteLineStdout("📥 Cloning…"u8);
            var cloneResult = GitClient.Clone(urlNs.AsView(), clone.AsView());
            int cloneCode = cloneResult.ExitCode;
            var stderr = NativeString.From(cloneResult.StdErr.AsView().Trim());
            cloneResult.Dispose();
            if (cloneCode != 0)
            {
                var msg = new NativeBuffer(clone.Length + 32 + stderr.Length);
                msg.Append(clone.AsView());
                msg.Append(" (clone failed: "u8);
                msg.Append(stderr.AsView());
                msg.AppendByte((byte)')');
                report.ManualRepos.Add(msg.Freeze());
                stderr.Dispose();
                clone.Dispose(); urlNs.Dispose();
                return;
            }
            stderr.Dispose();
        }
        else
        {
            cloneDotGit.Dispose();
        }

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
        bool hasLocalChanges = !status.StdOut.AsView().IsBlank();
        status.Dispose();
        if (hasLocalChanges)
        {
            var addResult = GitClient.AddAll(clone.AsView());
            int addCode = addResult.ExitCode;
            addResult.Dispose();
            var commitResult = GitClient.Commit(clone.AsView(), "dotai sync"u8);
            if (addCode != 0 || commitResult.ExitCode != 0)
            {
                commitResult.Dispose();
                var msg = new NativeBuffer(clone.Length + 20);
                msg.Append(clone.AsView());
                msg.Append(" (commit failed)"u8);
                report.ManualRepos.Add(msg.Freeze());
                clone.Dispose(); urlNs.Dispose();
                return;
            }
            commitResult.Dispose();
        }

        if (!silent) ConsoleOut.WriteLineStdout("🔍 Checking for changes…"u8);

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

        if (!silent)
        {
            var downRange = new NativeBuffer(upstream.Length + 8);
            downRange.Append("HEAD.."u8);
            downRange.Append(upstream.AsView());
            int downCount = GitClient.RevListCount(clone.AsView(), downRange.AsView());
            var downStat = downCount > 0 ? GitClient.ShortStat(clone.AsView(), downRange.AsView()) : default;
            downRange.Dispose();
            EmitChangesLine(pickingUp: true, count: downCount, stat: downStat);
        }

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
            var upstreamRef = new NativeBuffer(branchNs.Length + 8);
            upstreamRef.Append("origin/"u8);
            upstreamRef.Append(branchNs.AsView());
            var upRange = new NativeBuffer(upstreamRef.Length + 8);
            upRange.Append(upstreamRef.AsView());
            upRange.Append("..HEAD"u8);
            upstreamRef.Dispose();
            int upCount = GitClient.RevListCount(clone.AsView(), upRange.AsView());
            var upStat = upCount > 0 ? GitClient.ShortStat(clone.AsView(), upRange.AsView()) : default;
            upRange.Dispose();
            EmitChangesLine(pickingUp: false, count: upCount, stat: upStat);
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

        int skillsBefore = report.SkillsNew + report.SkillsUpdated + report.SkillsUnchanged;
        int filesBefore = report.FilesNew + report.FilesUpdated + report.FilesUnchanged;
        SkillLinker.LinkSkills(repoRoot, clone.AsView(), agents, ref report, force);
        SkillLinker.LinkFiles(repoRoot, clone.AsView(), ref report, force);
        SkillLinker.CleanupOrphans(repoRoot, agents, ref report);
        int contributed =
            (report.SkillsNew + report.SkillsUpdated + report.SkillsUnchanged - skillsBefore) +
            (report.FilesNew + report.FilesUpdated + report.FilesUnchanged - filesBefore);

        if (!silent)
        {
            if (contributed == 0) EmitEmptyRepoHint(repoRoot, nameView);
            ConsoleOut.WriteLineStdout("☑️  Sync complete"u8);
        }

        clone.Dispose();
        urlNs.Dispose();
    }

    private static void ReconcileRepositories(NativeStringView repoRoot, NativeListView<RepoConfig> config)
    {
        var aiDir = Fs.Combine(repoRoot, ".ai"u8);
        var reposDir = Fs.Combine(aiDir.AsView(), "repositories"u8);
        aiDir.Dispose();
        if (!Fs.IsDirectory(reposDir.AsView())) { reposDir.Dispose(); return; }

        // Active = names derived from config (new + legacy shape).
        var active = new NativeList<NativeString>(config.Length * 2);
        for (int i = 0; i < config.Length; i++)
        {
            active.Add(GitClient.DeriveCloneName(config[i].Name.AsView()));
            active.Add(GitClient.DeriveLegacyCloneName(config[i].Name.AsView()));
        }
        var protectedNames = SkillshareMigrator.LoadProtectedCloneNames(repoRoot);

        var entries = Fs.EnumerateDirectories(reposDir.AsView());
        for (int i = 0; i < entries.Length; i++)
        {
            var name = Fs.GetFileName(entries[i].AsView());
            bool keep = ContainsView(active.AsView(), name.AsView())
                || ContainsView(protectedNames.AsView(), name.AsView());
            if (!keep)
            {
                Fs.TryDeleteDirectoryRecursive(entries[i].AsView());
                var buf = new NativeBuffer(name.Length + 32);
                buf.Append("🗑️  Removed stale repository "u8);
                buf.Append(name.AsView());
                ConsoleOut.WriteLineStdout(buf.AsView());
                buf.Dispose();
            }
            name.Dispose();
        }
        for (int i = 0; i < entries.Length; i++) entries[i].Dispose();
        entries.Dispose();

        for (int i = 0; i < active.Length; i++) active[i].Dispose();
        active.Dispose();
        for (int i = 0; i < protectedNames.Length; i++) protectedNames[i].Dispose();
        protectedNames.Dispose();
        reposDir.Dispose();
    }

    private static bool ContainsView(NativeListView<NativeString> list, NativeStringView needle)
    {
        for (int i = 0; i < list.Length; i++)
            if (list[i].AsView().Bytes.SequenceEqual(needle.Bytes)) return true;
        return false;
    }

    private static void EmitEmptyRepoHint(NativeStringView repoRoot, NativeStringView spec)
    {
        var buf = new NativeBuffer(128 + spec.Length);
        buf.Append("⚠️  '"u8);
        buf.Append(spec);
        buf.Append("' provides no skills or files. Remove it from "u8);
        bool tty = Stdio.IsTty(1);
        var cfgAbs = Fs.Combine(repoRoot, ".ai/config.toml"u8);
        if (tty)
        {
            buf.Append("\x1b]8;;file://"u8);
            buf.Append(cfgAbs.AsView());
            buf.Append("\x1b\\"u8);
            buf.Append(".ai/config.toml"u8);
            buf.Append("\x1b]8;;\x1b\\"u8);
        }
        else
        {
            buf.Append(".ai/config.toml"u8);
        }
        buf.Append(" if it is no longer needed."u8);
        cfgAbs.Dispose();
        ConsoleOut.WriteLineStdout(buf.AsView());
        buf.Dispose();
    }

    private static void EmitRepoHeader(NativeStringView shortName, NativeStringView url)
    {
        bool tty = Stdio.IsTty(1);
        if (tty)
        {
            // 🗂️  <OSC8 url>cyan name</OSC8>
            Stdio.Write(1, "🗂️  "u8);
            Stdio.Write(1, "\x1b]8;;"u8);
            Stdio.Write(1, url);
            Stdio.Write(1, "\x1b\\"u8);
            Stdio.Write(1, "\x1b[36m"u8);
            Stdio.Write(1, shortName);
            Stdio.Write(1, "\x1b[0m"u8);
            Stdio.Write(1, "\x1b]8;;\x1b\\\n"u8);
        }
        else
        {
            Stdio.Write(1, "# "u8);
            Stdio.Write(1, shortName);
            Stdio.Write(1, " ("u8);
            Stdio.Write(1, url);
            Stdio.Write(1, ")\n"u8);
        }
    }

    private static void EmitChangesLine(bool pickingUp, int count, GitClient.DiffStat stat)
    {
        var buf = new NativeBuffer(96);
        buf.Append(pickingUp ? "📥 "u8 : "📤 "u8);
        if (count == 0)
        {
            buf.Append(pickingUp ? "No changes to pick"u8 : "No changes to apply"u8);
        }
        else
        {
            buf.Append(pickingUp ? "Picking up "u8 : "Applying "u8);
            buf.AppendInt(count);
            buf.Append(count == 1 ? " change (+"u8 : " changes (+"u8);
            buf.AppendInt(stat.Added);
            buf.Append("/-"u8);
            buf.AppendInt(stat.Deleted);
            buf.Append(")…"u8);
        }
        ConsoleOut.WriteLineStdout(buf.AsView());
        buf.Dispose();
    }

    private static void EmitCategoryLine(NativeStringView labelWithEmoji, int newCount, int updated, int gone)
    {
        if (newCount == 0 && updated == 0 && gone == 0) return;
        var buf = new NativeBuffer(128);
        buf.Append(labelWithEmoji);
        buf.AppendByte((byte)'\t');
        bool first = true;
        if (newCount > 0)
        {
            if (!first) buf.Append(" · "u8);
            buf.Append("✨ "u8);
            buf.AppendInt(newCount);
            buf.Append(" new"u8);
            first = false;
        }
        if (updated > 0)
        {
            if (!first) buf.Append(" · "u8);
            buf.Append("🌀 "u8);
            buf.AppendInt(updated);
            buf.Append(" updated"u8);
            first = false;
        }
        if (gone > 0)
        {
            if (!first) buf.Append(" · "u8);
            buf.Append("🗑️  "u8);
            buf.AppendInt(gone);
            buf.Append(" gone"u8);
        }
        ConsoleOut.WriteLineStdout(buf.AsView());
        buf.Dispose();
    }

    private static void EmitElapsed(long elapsedMs)
    {
        long totalSec = elapsedMs / 1000;
        long mins = totalSec / 60;
        long secs = totalSec % 60;
        var buf = new NativeBuffer(48);
        bool tty = Stdio.IsTty(1);
        if (tty) buf.Append("\x1b[90m"u8);
        buf.Append("(sync time: "u8);
        if (mins > 0)
        {
            buf.AppendInt((int)mins);
            buf.Append("m "u8);
        }
        buf.AppendInt((int)secs);
        buf.AppendByte((byte)'s');
        buf.AppendByte((byte)')');
        if (tty) buf.Append("\x1b[0m"u8);
        ConsoleOut.WriteLineStdout(buf.AsView());
        buf.Dispose();
    }
}
