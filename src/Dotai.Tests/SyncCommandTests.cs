using Dotai.Commands;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class SyncCommandTests
{
    private static string MakeProject(string baseDir, string remoteUrl, string? cloneNameOverride = null)
    {
        var repo = Path.Combine(baseDir, "project");
        Directory.CreateDirectory(repo);
        LocalGitRepo.Run(repo, "init", "--initial-branch=main");
        Directory.CreateDirectory(Path.Combine(repo, ".claude"));

        var aiDir = Path.Combine(repo, ".ai");
        var reposDir = Path.Combine(aiDir, "repositories");
        Directory.CreateDirectory(reposDir);
        var cloneKey = cloneNameOverride ?? GitClient.DeriveCloneName(remoteUrl);
        GitClient.Clone(remoteUrl, Path.Combine(reposDir, cloneKey));

        var config = new List<string>();
        ConfigStore.AddRepo(config, remoteUrl);
        ConfigStore.Save(Path.Combine(aiDir, "config.jsonc"), config);

        return repo;
    }

    [Fact]
    public void NoConfigReturnsError()
    {
        using var tmp = new TempDir();
        var repo = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(repo);
        LocalGitRepo.Run(repo, "init", "--initial-branch=main");
        var cmd = new SyncCommand(repo);

        var code = cmd.Execute(Array.Empty<string>());

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
        var project = MakeProject(tmp.Path, remoteUrl);
        var cmd = new SyncCommand(project);

        var code = cmd.Execute(Array.Empty<string>());

        Assert.Equal(0, code);
        Assert.True(Directory.Exists(Path.Combine(project, ".claude", "skills", "alpha")));
        Assert.NotNull(new FileInfo(Path.Combine(project, ".claude", "skills", "alpha")).LinkTarget);
        Assert.NotNull(cmd.LastReport);
        Assert.Equal(1, cmd.LastReport!.SkillsLinked);
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
        var project = MakeProject(tmp.Path, remoteUrl);
        var cloneKey = GitClient.DeriveCloneName(remoteUrl);
        var skillFile = Path.Combine(project, ".ai", "repositories", cloneKey, "skills", "alpha", "SKILL.md");
        File.WriteAllText(skillFile, "edited");
        var cmd = new SyncCommand(project);

        var code = cmd.Execute(Array.Empty<string>());

        Assert.Equal(0, code);
        var log = GitClient.Run(Path.Combine(project, ".ai", "repositories", cloneKey),
            "log", "--oneline");
        Assert.True(log.StdOut.AsSpan().IndexOf("dotai sync"u8) >= 0);
    }

    [Fact]
    public void ReportsRebaseInProgress()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "x"));
        var project = MakeProject(tmp.Path, remoteUrl);
        var cloneKey = GitClient.DeriveCloneName(remoteUrl);
        Directory.CreateDirectory(Path.Combine(project, ".ai", "repositories", cloneKey, ".git", "rebase-merge"));
        var cmd = new SyncCommand(project);

        var code = cmd.Execute(Array.Empty<string>());

        Assert.Equal(3, code);
    }

    [Fact]
    public void SkillshareWarningDoesNotBlockSync()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "x"));
        var project = MakeProject(tmp.Path, remoteUrl, GitClient.DeriveCloneName(remoteUrl));
        Directory.CreateDirectory(Path.Combine(project, ".skillshare"));
        var cmd = new SyncCommand(project);

        var code = cmd.Execute(Array.Empty<string>());

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
        File.WriteAllText(Path.Combine(aiDir, "config.jsonc"), "[\"not an object\"]");
        var cmd = new SyncCommand(repo);

        var code = cmd.Execute(Array.Empty<string>());

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
        File.WriteAllText(Path.Combine(aiDir, "config.jsonc"), "[\"not an object\"]");
        var cmd = new SyncCommand(repo);

        var code = cmd.Execute(new[] { "--force" });

        // after reset, config is empty → sync exits 1 "no repositories configured"
        Assert.Equal(1, code);
    }
}
