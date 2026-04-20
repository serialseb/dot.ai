using Dotai.Services;
using Dotai.Text;
using Dotai.Ui;

namespace Dotai.Commands;

public sealed class SyncCommand
{
    private readonly byte[] _startDir;

    public SyncCommand() : this(GetCwd()) { }
    private static byte[] GetCwd() { Fs.TryGetCurrentDirectory(out var d); return d; }
    public SyncCommand(byte[] startDir) { _startDir = startDir; }

    public bool Silent { get; init; }
    public bool Force { get; init; }

    public SyncReport? LastReport { get; private set; }

    public int Execute(Arg[] args)
    {
        if (!SharedFlags.TryParse(args, _startDir, out var parsed)) return 1;

        var rest = parsed.Positional;
        var startDir = parsed.StartDir;
        var force = parsed.Force || Force;

        if (rest.Length > 0 && rest[0].AsFast == "--help"u8)
        {
            ConsoleOut.Info(Help_u8);
            return 0;
        }

        if (!RepoRootResolver.TryFind(startDir, out var repoRoot))
        {
            ConsoleOut.Error("dotai requires a git repository"u8);
            return 1;
        }

        if (Fs.IsDirectory(Fs.Combine(repoRoot, ".skillshare"u8)))
            ConsoleOut.Warn(".skillshare present. Please uninstall or reconfigure."u8);

        var configPath = Fs.Combine(Fs.Combine(repoRoot, ".ai"u8), "config.jsonc"u8);
        if (!ConfigStore.TryLoad((FastString)configPath, out var config))
        {
            if (!force) return 2;
            config = new List<byte[]>();
            ConfigStore.Save((FastString)configPath, config);
            ConsoleOut.Warn("--force: reset malformed config. previous configuration lost."u8);
        }

        if (config.Count == 0)
        {
            ConsoleOut.Error("no repositories configured (run dotai init first)"u8);
            return 1;
        }

        var agentNames = AgentDetector.Detect((FastString)repoRoot);
        var agentArgs = new Arg[agentNames.Length];
        for (int i = 0; i < agentNames.Length; i++)
            agentArgs[i] = new Arg(agentNames[i]);

        if (force)
        {
            SkillLinker.ForceReset((FastString)repoRoot, agentArgs);
            ConsoleOut.Warn("--force: reset dotai-owned symlinks and config. previous dotai state lost."u8);
        }

        var report = new SyncReport();

        for (int i = 0; i < config.Count; i++)
        {
            SyncOne(repoRoot, config[i], agentArgs, report);
        }

        LastReport = report;

        if (!report.Ok)
        {
            ConsoleOut.Warn("completed with issues:"u8);
            for (int i = 0; i < report.ManualRepos.Count; i++)
            {
                var r = report.ManualRepos[i];
                var buf = new ByteBuffer(r.Length + 16);
                buf.Append("  \xe2\x80\xa2 manual: "u8);
                buf.Append(r);
                ConsoleOut.Detail(buf.Span);
            }
            for (int i = 0; i < report.Conflicts.Count; i++)
            {
                var c = report.Conflicts[i];
                var buf = new ByteBuffer(c.Length + 18);
                buf.Append("  \xe2\x80\xa2 conflict: "u8);
                buf.Append(c);
                ConsoleOut.Detail(buf.Span);
            }
            ConsoleOut.Hint("resolve the issues above, then run 'dotai sync' again"u8);
            return 3;
        }

        if (!Silent)
        {
            var plural = config.Count == 1 ? "repository"u8 : "repositories"u8;
            var buf = new ByteBuffer(64);
            buf.Append("synced "u8);
            buf.AppendInt(report.SkillsLinked);
            buf.Append(" skills, "u8);
            buf.AppendInt(report.FilesLinked);
            buf.Append(" files across "u8);
            buf.AppendInt(config.Count);
            buf.AppendByte((byte)' ');
            buf.Append(plural);
            ConsoleOut.Success(buf.Span);
        }
        return 0;
    }

    private static readonly byte[] Help_u8 =
        "dotai sync [standard flags] — sync all configured source repositories."u8.ToArray();

    private static void SyncOne(byte[] repoRoot, byte[] urlBytes, ReadOnlySpan<Arg> agents, SyncReport report)
    {
        FastString urlFast = urlBytes;
        var cloneNameBytes = GitClient.DeriveCloneName(urlFast);
        var clone = Fs.Combine(Fs.Combine(repoRoot, ".ai"u8), Fs.Combine("repositories"u8, cloneNameBytes));

        if (!Fs.IsDirectory(Fs.Combine(clone, ".git"u8)))
        {
            var msg = new ByteBuffer(clone.Length + 16);
            msg.Append(clone);
            msg.Append(" (not cloned)"u8);
            report.ManualRepos.Add(msg.Span.ToArray());
            return;
        }

        if (GitClient.RebaseInProgress((FastString)clone))
        {
            var msg = new ByteBuffer(clone.Length + 24);
            msg.Append(clone);
            msg.Append(" (rebase in progress)"u8);
            report.ManualRepos.Add(msg.Span.ToArray());
            return;
        }

        var status = GitClient.StatusPorcelain((FastString)clone);
        if (!ByteOps.IsBlank(status.StdOut))
        {
            if (GitClient.AddAll((FastString)clone).ExitCode != 0
                || GitClient.Commit((FastString)clone, (FastString)"dotai sync"u8).ExitCode != 0)
            {
                var msg = new ByteBuffer(clone.Length + 20);
                msg.Append(clone);
                msg.Append(" (commit failed)"u8);
                report.ManualRepos.Add(msg.Span.ToArray());
                return;
            }
        }

        if (GitClient.Fetch((FastString)clone).ExitCode != 0)
        {
            var msg = new ByteBuffer(clone.Length + 18);
            msg.Append(clone);
            msg.Append(" (fetch failed)"u8);
            report.ManualRepos.Add(msg.Span.ToArray());
            return;
        }

        var branchBytes = GitClient.DefaultBranch((FastString)clone);

        var upstream = new ByteBuffer(32);
        upstream.Append("origin/"u8);
        upstream.Append(branchBytes);
        var rebase = GitClient.Rebase((FastString)clone, (FastString)upstream.Span.ToArray());
        if (rebase.ExitCode != 0)
        {
            var msg = new ByteBuffer(clone.Length + 48);
            msg.Append(clone);
            msg.Append(" (rebase failed; resolve in .git/rebase-merge)"u8);
            report.ManualRepos.Add(msg.Span.ToArray());
            return;
        }

        var push = GitClient.Push((FastString)clone, (FastString)branchBytes);
        if (push.ExitCode != 0)
        {
            var stderrTrimmed = ByteOps.Trim(push.StdErr);
            var msg = new ByteBuffer(clone.Length + stderrTrimmed.Length + 20);
            msg.Append(clone);
            msg.Append(" (push failed: "u8);
            msg.Append(stderrTrimmed);
            msg.AppendByte((byte)')');
            report.ManualRepos.Add(msg.Span.ToArray());
            return;
        }

        var skillsBefore = report.SkillsLinked;
        var filesBefore = report.FilesLinked;
        SkillLinker.LinkSkills((FastString)repoRoot, (FastString)clone, agents, report);
        SkillLinker.LinkFiles((FastString)repoRoot, (FastString)clone, report);
        SkillLinker.CleanupOrphans((FastString)repoRoot, agents);
        var deltaSkills = report.SkillsLinked - skillsBefore;
        var deltaFiles = report.FilesLinked - filesBefore;

        var msgBuf = new ByteBuffer(64);
        msgBuf.Append("  \xe2\x80\xa2 "u8);
        msgBuf.Append(urlBytes);
        msgBuf.Append(": "u8);
        msgBuf.AppendInt(deltaSkills);
        msgBuf.Append(" skills, "u8);
        msgBuf.AppendInt(deltaFiles);
        msgBuf.Append(" files"u8);
        ConsoleOut.Info(msgBuf.Span);
    }
}
