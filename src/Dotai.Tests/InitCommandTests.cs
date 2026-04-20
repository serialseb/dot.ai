using System.Text;
using Dotai.Commands;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class InitCommandTests
{
    private static string MakeGitRepoWithAgent(string baseDir)
    {
        var repo = Path.Combine(baseDir, "project");
        Directory.CreateDirectory(repo);
        LocalGitRepo.Run(repo, "init", "--initial-branch=main");
        Directory.CreateDirectory(Path.Combine(repo, ".claude"));
        return repo;
    }

    [Fact]
    public void RejectsWhenNotInsideGitRepo()
    {
        using var tmp = new TempDir();
        var cmd = new InitCommand(tmp.Path);

        var code = cmd.Execute(new[] { "owner/repo" });

        Assert.Equal(1, code);
    }

    [Fact]
    public void RejectsMalformedArg()
    {
        using var tmp = new TempDir();
        var repo = MakeGitRepoWithAgent(tmp.Path);
        var cmd = new InitCommand(repo);

        var code = cmd.Execute(new[] { "not-a-valid-arg" });

        Assert.Equal(1, code);
    }

    [Fact]
    public void CreatesAiTreeAndConfigAndClonesRepo()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
        {
            var skill = Path.Combine(w, "skills", "alpha");
            Directory.CreateDirectory(skill);
            File.WriteAllText(Path.Combine(skill, "SKILL.md"), "hi");
        });
        var repo = MakeGitRepoWithAgent(tmp.Path);
        var cmd = new InitCommand(repo) { CloneUrlOverride = remoteUrl };

        var code = cmd.Execute(new[] { "owner/repo" });

        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(repo, ".ai", "config.jsonc")));
        Assert.True(File.Exists(Path.Combine(repo, ".ai", ".gitignore")));
        // PHASE3-TEMP: DeriveCloneName takes FastString; convert once at test boundary.
        var cloneKey = Encoding.UTF8.GetString(GitClient.DeriveCloneName(Encoding.UTF8.GetBytes(remoteUrl)));
        Assert.True(Directory.Exists(Path.Combine(repo, ".ai", "repositories", cloneKey, ".git")));
        var link = Path.Combine(repo, ".claude", "skills", "alpha");
        Assert.True(Directory.Exists(link));
        Assert.NotNull(new FileInfo(link).LinkTarget);
    }

    [Fact]
    public void RespectsDashProjectFlag()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
        {
            var skill = Path.Combine(w, "skills", "alpha");
            Directory.CreateDirectory(skill);
            File.WriteAllText(Path.Combine(skill, "SKILL.md"), "hi");
        });
        var repo = MakeGitRepoWithAgent(tmp.Path);
        var foreign = Path.Combine(tmp.Path, "unrelated-cwd");
        Directory.CreateDirectory(foreign);
        var cmd = new InitCommand(foreign) { CloneUrlOverride = remoteUrl };

        var code = cmd.Execute(new[] { "-p", repo, "owner/repo" });

        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(repo, ".ai", "config.jsonc")));
    }
}
