using System.Text;
using Dotai.Services;
using Dotai.Text;
using Dotai.Ui;

namespace Dotai.Commands;

public sealed class SyncCommand : ICommand
{
    private readonly string _startDir;

    public SyncCommand() : this(Directory.GetCurrentDirectory()) { }
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

        var repoRoot = RepoRootResolver.Find(startDir);
        if (repoRoot == null)
        {
            ConsoleOut.Error("dotai requires a git repository"u8);
            return 1;
        }

        if (Directory.Exists(Path.Combine(repoRoot, ".skillshare")))
            ConsoleOut.Warn(".skillshare present. Please uninstall or reconfigure."u8);

        var configPath = Path.Combine(repoRoot, ".ai", "config.jsonc");
        List<byte[]> config;
        try
        {
            config = ConfigStore.Load(configPath);
        }
        catch (InvalidDataException)
        {
            if (!force)
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

        if (config.Count == 0)
        {
            ConsoleOut.Error("no repositories configured (run dotai init first)"u8);
            return 1;
        }

        var agents = AgentDetector.Detect(repoRoot);

        if (force)
        {
            SkillLinker.ForceReset(repoRoot, agents);
            ConsoleOut.Warn("--force: reset dotai-owned symlinks and config. previous dotai state lost."u8);
        }

        var report = new SyncReport();

        foreach (var urlBytes in config)
        {
            SyncOne(repoRoot, urlBytes, agents, report);
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

    private static void SyncOne(string repoRoot, byte[] urlBytes, ReadOnlySpan<string> agents, SyncReport report)
    {
        FastString urlFast = urlBytes;
        var cloneNameBytes = GitClient.DeriveCloneName(urlFast);
        // PHASE3-TEMP: path APIs still take string; Phase 3b will use libc.
        var cloneName = Encoding.UTF8.GetString(cloneNameBytes);
        var clone = Path.Combine(repoRoot, ".ai", "repositories", cloneName);
        if (!Directory.Exists(Path.Combine(clone, ".git")))
        {
            report.ManualRepos.Add($"{clone} (not cloned)");
            return;
        }

        if (GitClient.RebaseInProgress(clone))
        {
            report.ManualRepos.Add($"{clone} (rebase in progress)");
            return;
        }

        var status = GitClient.StatusPorcelain(clone);
        if (!ByteOps.IsBlank(status.StdOut))
        {
            if (GitClient.AddAll(clone).ExitCode != 0
                || GitClient.Commit(clone, "dotai sync").ExitCode != 0)
            {
                report.ManualRepos.Add($"{clone} (commit failed)");
                return;
            }
        }

        if (GitClient.Fetch(clone).ExitCode != 0)
        {
            report.ManualRepos.Add($"{clone} (fetch failed)");
            return;
        }

        var branchBytes = GitClient.DefaultBranch(clone);

        // Compose "origin/<branch>" via ByteBuffer — no string encoding needed.
        var upstream = new ByteBuffer(32);
        upstream.Append("origin/"u8);
        upstream.Append(branchBytes);
        // PHASE3-TEMP: Rebase still takes string upstream; Phase 3b will flip the signature.
        var rebase = GitClient.Rebase(clone, Encoding.UTF8.GetString(upstream.Span));
        if (rebase.ExitCode != 0)
        {
            report.ManualRepos.Add($"{clone} (rebase failed; resolve in .git/rebase-merge)");
            return;
        }

        // PHASE3-TEMP: Push still takes string branch; Phase 3b will flip the signature.
        var push = GitClient.Push(clone, Encoding.UTF8.GetString(branchBytes));
        if (push.ExitCode != 0)
        {
            var stderrTrimmed = ByteOps.Trim(push.StdErr);
            // TEMP(Phase3c): push error message still stored as string for ManualRepos list.
            report.ManualRepos.Add($"{clone} (push failed: {Encoding.UTF8.GetString(stderrTrimmed)})");
            return;
        }

        var skillsBefore = report.SkillsLinked;
        var filesBefore = report.FilesLinked;
        SkillLinker.LinkSkills(repoRoot, clone, agents, report);
        SkillLinker.LinkFiles(repoRoot, clone, report);
        SkillLinker.CleanupOrphans(repoRoot, agents);
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
