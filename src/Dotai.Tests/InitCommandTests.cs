using System.Text;
using Dotai.Commands;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Dotai.Text;
using Xunit;

namespace Dotai.Tests;

public class InitCommandTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);
    private static Arg[] Args(params string[] ss) { var r = new Arg[ss.Length]; for (int i = 0; i < ss.Length; i++) r[i] = new Arg(B(ss[i])); return r; }

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
        var cmd = new InitCommand(B(tmp.Path));

        var code = cmd.Execute(Args("owner/repo"));

        Assert.Equal(1, code);
    }

    [Fact]
    public void RejectsMalformedArg()
    {
        using var tmp = new TempDir();
        var repo = MakeGitRepoWithAgent(tmp.Path);
        var cmd = new InitCommand(B(repo));

        var code = cmd.Execute(Args("not-a-valid-arg"));

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
        var cmd = new InitCommand(B(repo)) { CloneUrlOverride = B(remoteUrl) };

        var code = cmd.Execute(Args("owner/repo"));

        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(repo, ".ai", "config.jsonc")));
        Assert.True(File.Exists(Path.Combine(repo, ".ai", ".gitignore")));
        var cloneKey = Encoding.UTF8.GetString(GitClient.DeriveCloneName((FastString)B(remoteUrl)));
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
        var cmd = new InitCommand(B(foreign)) { CloneUrlOverride = B(remoteUrl) };

        var code = cmd.Execute(Args("-p", repo, "owner/repo"));

        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(repo, ".ai", "config.jsonc")));
    }
}
