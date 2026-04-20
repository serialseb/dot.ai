using System.Text.Json;
using Dotai.Services;
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
        catch (ArgumentException ex) { ConsoleOut.Error(ex.Message); return 1; }

        var rest = parsed.Positional;
        var startDir = parsed.StartDir;
        var force = parsed.Force || Force;

        if (rest.Length > 0 && rest[0] == "--help")
        {
            ConsoleOut.Info(Help);
            return 0;
        }

        var repoRoot = RepoRootResolver.Find(startDir);
        if (repoRoot == null)
        {
            ConsoleOut.Error("dotai requires a git repository");
            return 1;
        }

        if (Directory.Exists(Path.Combine(repoRoot, ".skillshare")))
            ConsoleOut.Warn(".skillshare present. Please uninstall or reconfigure.");

        var configPath = Path.Combine(repoRoot, ".ai", "config.jsonc");
        Dictionary<string, JsonElement> config;
        try
        {
            config = ConfigStore.Load(configPath);
        }
        catch (InvalidDataException)
        {
            if (!force)
            {
                ConsoleOut.Error($"config at {configPath} is malformed. Fix the file, or rerun with --force to reset (all previous configuration will be lost).");
                return 2;
            }
            config = new Dictionary<string, JsonElement>();
            ConfigStore.Save(configPath, config);
            ConsoleOut.Warn("--force: reset malformed config. previous configuration lost.");
        }

        if (config.Count == 0)
        {
            ConsoleOut.Error("no repositories configured (run dotai init first)");
            return 1;
        }

        var agents = AgentDetector.Detect(repoRoot);

        if (force)
        {
            SkillLinker.ForceReset(repoRoot, agents);
            ConsoleOut.Warn("--force: reset dotai-owned symlinks and config. previous dotai state lost.");
        }

        var report = new SyncReport();

        foreach (var (url, _) in config)
        {
            SyncOne(repoRoot, url, agents, report);
        }

        LastReport = report;

        if (!report.Ok)
        {
            ConsoleOut.Warn("completed with issues:");
            foreach (var r in report.ManualRepos) ConsoleOut.Detail($"  • manual: {r}");
            foreach (var c in report.Conflicts) ConsoleOut.Detail($"  • conflict: {c}");
            ConsoleOut.Hint("resolve the issues above, then run 'dotai sync' again");
            return 3;
        }

        if (!Silent)
        {
            var plural = config.Count == 1 ? "repository" : "repositories";
            ConsoleOut.Success(
                $"synced {report.SkillsLinked} skills, {report.FilesLinked} files across {config.Count} {plural}");
        }
        return 0;
    }

    private static void SyncOne(string repoRoot, string url, IReadOnlyList<string> agents, SyncReport report)
    {
        var cloneName = GitClient.DeriveCloneName(url);
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
        if (!string.IsNullOrWhiteSpace(status.StdOut))
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

        var branch = GitClient.DefaultBranch(clone);
        var rebase = GitClient.Rebase(clone, $"origin/{branch}");
        if (rebase.ExitCode != 0)
        {
            report.ManualRepos.Add($"{clone} (rebase failed; resolve in .git/rebase-merge)");
            return;
        }

        var push = GitClient.Push(clone, branch);
        if (push.ExitCode != 0)
        {
            report.ManualRepos.Add($"{clone} (push failed: {push.StdErr.Trim()})");
            return;
        }

        var skillsBefore = report.SkillsLinked;
        var filesBefore = report.FilesLinked;
        SkillLinker.LinkSkills(repoRoot, clone, agents, report);
        SkillLinker.LinkFiles(repoRoot, clone, report);
        SkillLinker.CleanupOrphans(repoRoot, agents);
        var deltaSkills = report.SkillsLinked - skillsBefore;
        var deltaFiles = report.FilesLinked - filesBefore;
        ConsoleOut.Info($"  • {url}: {deltaSkills} skills, {deltaFiles} files");
    }

}
