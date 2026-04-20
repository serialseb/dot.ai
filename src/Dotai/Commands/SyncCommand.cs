using System.Text;
using Dotai.Services;
using Dotai.Text;
using Dotai.Ui;

namespace Dotai.Commands;

public sealed class SyncCommand : ICommand
{
    private readonly string _startDir;

    public SyncCommand() : this(Encoding.UTF8.GetString(Fs.GetCurrentDirectory())) { }
    public SyncCommand(string startDir) { _startDir = startDir; }

    public bool Silent { get; init; }
    public bool Force { get; init; }

    public string Name => "sync";
    public string Help => "dotai sync [standard flags] — sync all configured source repositories.";

    public SyncReport? LastReport { get; private set; }

    public int Execute(string[] args)
    {
        ParsedArgs parsed;
        try { parsed = SharedFlags.Parse(args, _startDir); }
        catch (ArgumentException ex)
        {
            // TEMP(Phase3c): ex.Message is a string; Phase 3c will propagate byte errors.
            ConsoleOut.Error(Encoding.UTF8.GetBytes(ex.Message));
            return 1;
        }

        var rest = parsed.Positional;
        var startDir = parsed.StartDir;
        var force = parsed.Force || Force;

        if (rest.Length > 0 && rest[0] == "--help")
        {
            ConsoleOut.Info(Help_u8);
            return 0;
        }

        var startDirBytes = Encoding.UTF8.GetBytes(startDir);
        if (!RepoRootResolver.TryFind(startDirBytes, out var repoRoot))
        {
            ConsoleOut.Error("dotai requires a git repository"u8);
            return 1;
        }

        if (Fs.IsDirectory(Fs.Combine(repoRoot, ".skillshare"u8)))
            ConsoleOut.Warn(".skillshare present. Please uninstall or reconfigure."u8);

        var configPath = Fs.Combine(Fs.Combine(repoRoot, ".ai"u8), "config.jsonc"u8);
        List<byte[]> config;
        try
        {
            config = ConfigStore.Load((FastString)configPath);
        }
        catch (InvalidDataException)
        {
            if (!force)
            {
                var buf = new ByteBuffer(128);
                buf.Append("config at "u8);
                buf.Append(configPath);
                buf.Append(" is malformed. Fix the file, or rerun with --force to reset (all previous configuration will be lost)."u8);
                ConsoleOut.Error(buf.Span);
                return 2;
            }
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
        // PHASE3-TEMP: SkillLinker still takes string[]; Phase 3c will flip.
        var agentStrings = new string[agentNames.Length];
        for (int i = 0; i < agentNames.Length; i++)
            agentStrings[i] = Encoding.UTF8.GetString(agentNames[i]);

        if (force)
        {
            SkillLinker.ForceReset(Encoding.UTF8.GetString(repoRoot), agentStrings);
            ConsoleOut.Warn("--force: reset dotai-owned symlinks and config. previous dotai state lost."u8);
        }

        var report = new SyncReport();

        foreach (var urlBytes in config)
        {
            SyncOne(repoRoot, urlBytes, agentStrings, report);
        }

        LastReport = report;

        if (!report.Ok)
        {
            ConsoleOut.Warn("completed with issues:"u8);
            foreach (var r in report.ManualRepos)
            {
                var buf = new ByteBuffer(64);
                buf.Append("  \xe2\x80\xa2 manual: "u8);
                // TEMP(Phase3c): r is string; Phase 3c will propagate byte messages.
                buf.Append(Encoding.UTF8.GetBytes(r));
                ConsoleOut.Detail(buf.Span);
            }
            foreach (var c in report.Conflicts)
            {
                var buf = new ByteBuffer(64);
                buf.Append("  \xe2\x80\xa2 conflict: "u8);
                // TEMP(Phase3c): c is string; Phase 3c will propagate byte messages.
                buf.Append(Encoding.UTF8.GetBytes(c));
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

    private static void SyncOne(byte[] repoRoot, byte[] urlBytes, ReadOnlySpan<string> agents, SyncReport report)
    {
        FastString urlFast = urlBytes;
        var cloneNameBytes = GitClient.DeriveCloneName(urlFast);
        var cloneName = Encoding.UTF8.GetString(cloneNameBytes);
        var clone = Fs.Combine(Fs.Combine(repoRoot, ".ai"u8), Fs.Combine("repositories"u8, cloneNameBytes));
        var cloneStr = Encoding.UTF8.GetString(clone);

        if (!Fs.IsDirectory(Fs.Combine(clone, ".git"u8)))
        {
            report.ManualRepos.Add($"{cloneStr} (not cloned)");
            return;
        }

        if (GitClient.RebaseInProgress(cloneStr))
        {
            report.ManualRepos.Add($"{cloneStr} (rebase in progress)");
            return;
        }

        var status = GitClient.StatusPorcelain(cloneStr);
        if (!ByteOps.IsBlank(status.StdOut))
        {
            if (GitClient.AddAll(cloneStr).ExitCode != 0
                || GitClient.Commit(cloneStr, "dotai sync").ExitCode != 0)
            {
                report.ManualRepos.Add($"{cloneStr} (commit failed)");
                return;
            }
        }

        if (GitClient.Fetch(cloneStr).ExitCode != 0)
        {
            report.ManualRepos.Add($"{cloneStr} (fetch failed)");
            return;
        }

        var branchBytes = GitClient.DefaultBranch(cloneStr);

        var upstream = new ByteBuffer(32);
        upstream.Append("origin/"u8);
        upstream.Append(branchBytes);
        // PHASE3-TEMP: Rebase still takes string upstream; Phase 3c will flip the signature.
        var rebase = GitClient.Rebase(cloneStr, Encoding.UTF8.GetString(upstream.Span));
        if (rebase.ExitCode != 0)
        {
            report.ManualRepos.Add($"{cloneStr} (rebase failed; resolve in .git/rebase-merge)");
            return;
        }

        // PHASE3-TEMP: Push still takes string branch; Phase 3c will flip the signature.
        var push = GitClient.Push(cloneStr, Encoding.UTF8.GetString(branchBytes));
        if (push.ExitCode != 0)
        {
            var stderrTrimmed = ByteOps.Trim(push.StdErr);
            report.ManualRepos.Add($"{cloneStr} (push failed: {Encoding.UTF8.GetString(stderrTrimmed)})");
            return;
        }

        var repoRootStr = Encoding.UTF8.GetString(repoRoot);
        var skillsBefore = report.SkillsLinked;
        var filesBefore = report.FilesLinked;
        SkillLinker.LinkSkills(repoRootStr, cloneStr, agents, report);
        SkillLinker.LinkFiles(repoRootStr, cloneStr, report);
        SkillLinker.CleanupOrphans(repoRootStr, agents);
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
