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

    private static string MakeProject(string baseDir, string remoteUrl, string? cloneNameOverride = null)
    {
        var repo = Path.Combine(baseDir, "project");
        Directory.CreateDirectory(repo);
        LocalGitRepo.Run(repo, "init", "--initial-branch=main");
        Directory.CreateDirectory(Path.Combine(repo, ".claude"));

        var aiDir = Path.Combine(repo, ".ai");
        var reposDir = Path.Combine(aiDir, "repositories");
        Directory.CreateDirectory(reposDir);
        var cloneNameNs = GitClient.DeriveCloneName(V(remoteUrl));
        var cloneKey = cloneNameOverride ?? Encoding.UTF8.GetString(cloneNameNs.AsView().Bytes);
        cloneNameNs.Dispose();
        GitClient.Clone(V(remoteUrl), V(Path.Combine(reposDir, cloneKey))).Dispose();

        var config = new NativeList<NativeString>(4);
        ConfigStore.AddRepo(ref config, V(remoteUrl));
        ConfigStore.Save(V(Path.Combine(aiDir, "config.jsonc")), config);
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
        var project = MakeProject(tmp.Path, remoteUrl);
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
        var project = MakeProject(tmp.Path, remoteUrl);
        var cloneKeyNs = GitClient.DeriveCloneName(V(remoteUrl));
        var cloneKey = Encoding.UTF8.GetString(cloneKeyNs.AsView().Bytes);
        cloneKeyNs.Dispose();
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
        var project = MakeProject(tmp.Path, remoteUrl);
        var cloneKeyNs = GitClient.DeriveCloneName(V(remoteUrl));
        var cloneKey = Encoding.UTF8.GetString(cloneKeyNs.AsView().Bytes);
        cloneKeyNs.Dispose();
        Directory.CreateDirectory(Path.Combine(project, ".ai", "repositories", cloneKey, ".git", "rebase-merge"));
        var cmd = new SyncCommand(V(project));
        var emptyArgs = EmptyArgs();

        var code = cmd.Execute(emptyArgs.AsView());
        emptyArgs.Dispose();

        Assert.Equal(3, code);
    }

    [Fact]
    public void SkillshareWarningDoesNotBlockSync()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "x"));
        var cloneKeyNs = GitClient.DeriveCloneName(V(remoteUrl));
        var cloneNameOverride = Encoding.UTF8.GetString(cloneKeyNs.AsView().Bytes);
        cloneKeyNs.Dispose();
        var project = MakeProject(tmp.Path, remoteUrl, cloneNameOverride);
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
        File.WriteAllText(Path.Combine(aiDir, "config.jsonc"), "[\"not an object\"]");
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
        File.WriteAllText(Path.Combine(aiDir, "config.jsonc"), "[\"not an object\"]");
        var cmd = new SyncCommand(V(repo));
        var forceArgs = Args("--force");

        var code = cmd.Execute(forceArgs.AsView());
        for (int i = 0; i < forceArgs.Length; i++) forceArgs[i].Dispose();
        forceArgs.Dispose();

        // after reset, config is empty → sync exits 1 "no repositories configured"
        Assert.Equal(1, code);
    }
}
