using System.Text;
using Dotai.Commands;
using Dotai.Native;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class SyncCommandTests
{
    private static NativeStringView V(string s) => Encoding.UTF8.GetBytes(s);

    private static NativeList<NativeString> Args(params string[] ss)
    {
        var r = new NativeList<NativeString>(ss.Length > 0 ? ss.Length : 1);
        for (int i = 0; i < ss.Length; i++) r.Add(NativeString.From(V(ss[i])));
        return r;
    }

    private static NativeList<NativeString> EmptyArgs()
        => new NativeList<NativeString>(1);

    /// <summary>
    /// Creates a project with a cloned remote and a config.toml using
    /// ownerRepo ("owner/repo") as the registered name.
    /// </summary>
    private static string MakeProject(string baseDir, string remoteUrl, string ownerRepo, string? cloneNameOverride = null)
    {
        var repo = Path.Combine(baseDir, "project");
        Directory.CreateDirectory(repo);
        LocalGitRepo.Run(repo, "init", "--initial-branch=main");
        Directory.CreateDirectory(Path.Combine(repo, ".claude"));

        var aiDir = Path.Combine(repo, ".ai");
        var reposDir = Path.Combine(aiDir, "repositories");
        Directory.CreateDirectory(reposDir);

        // Derive clone key from owner/repo (replaces '/' with '_')
        var cloneNameNs = GitClient.DeriveCloneName(V(ownerRepo));
        var cloneKey = cloneNameOverride ?? Encoding.UTF8.GetString(cloneNameNs.AsView().Bytes);
        cloneNameNs.Dispose();
        GitClient.Clone(V(remoteUrl), V(Path.Combine(reposDir, cloneKey))).Dispose();

        var config = new NativeList<RepoConfig>(4);
        ConfigStore.AddRepo(ref config, V(ownerRepo), "merge"u8);
        ConfigStore.Save(V(Path.Combine(aiDir, "config.toml")), config.AsView());
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();

        return repo;
    }

    [Fact]
    public void NoConfigReturnsError()
    {
        using var tmp = new TempDir();
        var repo = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(repo);
        LocalGitRepo.Run(repo, "init", "--initial-branch=main");
        var cmd = new SyncCommand(V(repo));
        var emptyArgs = EmptyArgs();

        var code = cmd.Execute(emptyArgs.AsView());
        emptyArgs.Dispose();

        Assert.Equal(1, code);
    }

    [Fact]
    public void LinksSkillsFromCleanClone()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
        {
            Directory.CreateDirectory(Path.Combine(w, "skills", "alpha"));
            File.WriteAllText(Path.Combine(w, "skills", "alpha", "SKILL.md"), "x");
        });
        var project = MakeProject(tmp.Path, remoteUrl, "owner/repo");
        var cmd = new SyncCommand(V(project));
        var emptyArgs = EmptyArgs();

        var code = cmd.Execute(emptyArgs.AsView());
        emptyArgs.Dispose();

        Assert.Equal(0, code);
        Assert.True(Directory.Exists(Path.Combine(project, ".claude", "skills", "alpha")));
        Assert.NotNull(new FileInfo(Path.Combine(project, ".claude", "skills", "alpha")).LinkTarget);
        Assert.True(cmd.HasLastReport);
        Assert.Equal(1, cmd.LastReport.SkillsLinked);
    }

    [Fact]
    public void CommitsLocalChangesAndPushes()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
        {
            Directory.CreateDirectory(Path.Combine(w, "skills", "alpha"));
            File.WriteAllText(Path.Combine(w, "skills", "alpha", "SKILL.md"), "x");
        });
        var project = MakeProject(tmp.Path, remoteUrl, "owner/repo");
        var cloneKey = "github.com▸owner▸repo";
        var skillFile = Path.Combine(project, ".ai", "repositories", cloneKey, "skills", "alpha", "SKILL.md");
        File.WriteAllText(skillFile, "edited");
        var cmd = new SyncCommand(V(project));
        var emptyArgs = EmptyArgs();

        var code = cmd.Execute(emptyArgs.AsView());
        emptyArgs.Dispose();

        Assert.Equal(0, code);
        var cloneDir = Path.Combine(project, ".ai", "repositories", cloneKey);
        var log = GitClient.Run(V(cloneDir), "log"u8, "--oneline"u8);
        Assert.True(log.StdOut.AsView().IndexOf("dotai sync"u8) >= 0);
        log.Dispose();
    }

    [Fact]
    public void ReportsRebaseInProgress()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "x"));
        var project = MakeProject(tmp.Path, remoteUrl, "owner/repo");
        var cloneKey = "github.com▸owner▸repo";
        Directory.CreateDirectory(Path.Combine(project, ".ai", "repositories", cloneKey, ".git", "rebase-merge"));
        var cmd = new SyncCommand(V(project));
        var emptyArgs = EmptyArgs();

        var code = cmd.Execute(emptyArgs.AsView());
        emptyArgs.Dispose();

        Assert.Equal(3, code);
    }

    [Fact]
    public void RenamesLegacyUnderscoreCloneDirOnSync()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
        {
            Directory.CreateDirectory(Path.Combine(w, "skills", "alpha"));
            File.WriteAllText(Path.Combine(w, "skills", "alpha", "SKILL.md"), "x");
        });
        // Use the override to set up a clone under the legacy '_'-joined name.
        var project = MakeProject(tmp.Path, remoteUrl, "owner/repo", cloneNameOverride: "owner_repo");
        var legacy = Path.Combine(project, ".ai", "repositories", "owner_repo");
        var modern = Path.Combine(project, ".ai", "repositories", "github.com▸owner▸repo");
        Assert.True(Directory.Exists(legacy));
        Assert.False(Directory.Exists(modern));

        var cmd = new SyncCommand(V(project));
        var emptyArgs = EmptyArgs();
        var code = cmd.Execute(emptyArgs.AsView());
        emptyArgs.Dispose();

        Assert.Equal(0, code);
        Assert.False(Directory.Exists(legacy));
        Assert.True(Directory.Exists(modern));
    }

    [Fact]
    public void SkillshareWarningDoesNotBlockSync()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "x"));
        var project = MakeProject(tmp.Path, remoteUrl, "owner/repo");
        Directory.CreateDirectory(Path.Combine(project, ".skillshare"));
        var cmd = new SyncCommand(V(project));
        var emptyArgs = EmptyArgs();

        var code = cmd.Execute(emptyArgs.AsView());
        emptyArgs.Dispose();

        Assert.Equal(0, code);
    }

    [Fact]
    public void MalformedConfigErrorsWithoutForce()
    {
        using var tmp = new TempDir();
        var repo = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(repo);
        LocalGitRepo.Run(repo, "init", "--initial-branch=main");
        var aiDir = Path.Combine(repo, ".ai");
        Directory.CreateDirectory(aiDir);
        File.WriteAllText(Path.Combine(aiDir, "config.toml"), "[unclosed section\nmode = \"merge\"\n");
        var cmd = new SyncCommand(V(repo));
        var emptyArgs = EmptyArgs();

        var code = cmd.Execute(emptyArgs.AsView());
        emptyArgs.Dispose();

        Assert.Equal(2, code);
    }

    [Fact]
    public void MalformedConfigResetsWithForce()
    {
        using var tmp = new TempDir();
        var repo = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(repo);
        LocalGitRepo.Run(repo, "init", "--initial-branch=main");
        var aiDir = Path.Combine(repo, ".ai");
        Directory.CreateDirectory(aiDir);
        File.WriteAllText(Path.Combine(aiDir, "config.toml"), "[unclosed section\nmode = \"merge\"\n");
        var cmd = new SyncCommand(V(repo));
        var forceArgs = Args("--force");

        var code = cmd.Execute(forceArgs.AsView());
        for (int i = 0; i < forceArgs.Length; i++) forceArgs[i].Dispose();
        forceArgs.Dispose();

        // after reset, config is empty → sync exits 1 "no repositories configured"
        Assert.Equal(1, code);
    }

    [Fact]
    public void DeletesUnreferencedCloneDirOnSync()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
        {
            Directory.CreateDirectory(Path.Combine(w, "skills", "alpha"));
            File.WriteAllText(Path.Combine(w, "skills", "alpha", "SKILL.md"), "x");
        });
        var project = MakeProject(tmp.Path, remoteUrl, "owner/repo");

        // Simulate a previously-tracked repo whose entry has since been
        // removed from config.toml: the clone directory is still there but
        // no longer appears in config.
        var stale = Path.Combine(project, ".ai", "repositories", "example.com▸ghost▸gone");
        Directory.CreateDirectory(Path.Combine(stale, ".git"));

        var cmd = new SyncCommand(V(project));
        var args = EmptyArgs();
        var code = cmd.Execute(args.AsView());
        args.Dispose();

        Assert.Equal(0, code);
        Assert.False(Directory.Exists(stale));
    }

    [Fact]
    public void PreservesMigratedCloneEvenWithoutConfigEntry()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
        {
            Directory.CreateDirectory(Path.Combine(w, "skills", "alpha"));
            File.WriteAllText(Path.Combine(w, "skills", "alpha", "SKILL.md"), "x");
        });
        var project = MakeProject(tmp.Path, remoteUrl, "owner/repo");

        // A clone from a past Skillshare migration. Even though config.toml
        // does not list it, skillshare.toml records it as protected.
        var migratedName = "github.com▸foo▸bar";
        var migratedDir = Path.Combine(project, ".ai", "repositories", migratedName);
        Directory.CreateDirectory(Path.Combine(migratedDir, ".git"));

        var migrationDir = Path.Combine(project, ".ai", "migration");
        Directory.CreateDirectory(migrationDir);
        File.WriteAllText(Path.Combine(migrationDir, "skillshare.toml"),
            $"[[repository]]\nnew_name = \"{migratedName}\"\nformer_path = \".skillshare/skills/_foo_bar\"\n");

        var cmd = new SyncCommand(V(project));
        var args = EmptyArgs();
        var code = cmd.Execute(args.AsView());
        args.Dispose();

        Assert.Equal(0, code);
        Assert.True(Directory.Exists(migratedDir));
    }
}
