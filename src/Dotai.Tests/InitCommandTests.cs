using System.Text;
using Dotai.Commands;
using Dotai.Native;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class InitCommandTests
{
    private static NativeStringView V(string s) => Encoding.UTF8.GetBytes(s);

    private static NativeList<NativeString> Args(params string[] ss)
    {
        var r = new NativeList<NativeString>(ss.Length > 0 ? ss.Length : 1);
        for (int i = 0; i < ss.Length; i++) r.Add(NativeString.From(V(ss[i])));
        return r;
    }

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
        var cmd = new InitCommand(V(tmp.Path));
        var args = Args("owner/repo");

        var code = cmd.Execute(args.AsView());
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();

        Assert.Equal(1, code);
    }

    [Fact]
    public void RejectsMalformedArg()
    {
        using var tmp = new TempDir();
        var repo = MakeGitRepoWithAgent(tmp.Path);
        var cmd = new InitCommand(V(repo));
        var args = Args("not-a-valid-arg");

        var code = cmd.Execute(args.AsView());
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();

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
        var cmd = new InitCommand(V(repo)) { CloneUrlOverride = NativeString.From(V(remoteUrl)) };
        var args = Args("owner/repo");

        var code = cmd.Execute(args.AsView());
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();

        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(repo, ".ai", "config.toml")));
        Assert.True(File.Exists(Path.Combine(repo, ".ai", ".gitignore")));
        // Clone name is derived from owner/repo → github.com▸owner▸repo
        Assert.True(Directory.Exists(Path.Combine(repo, ".ai", "repositories", "github.com▸owner▸repo", ".git")));
        var link = Path.Combine(repo, ".claude", "skills", "alpha");
        Assert.True(Directory.Exists(link));
        Assert.NotNull(new FileInfo(link).LinkTarget);
    }

    [Fact]
    public void AcceptsHostFormAndCreatesHostPrefixedCloneDir()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
        {
            var skill = Path.Combine(w, "skills", "alpha");
            Directory.CreateDirectory(skill);
            File.WriteAllText(Path.Combine(skill, "SKILL.md"), "hi");
        });
        var repo = MakeGitRepoWithAgent(tmp.Path);
        var cmd = new InitCommand(V(repo)) { CloneUrlOverride = NativeString.From(V(remoteUrl)) };
        var args = Args("gitlab.com/owner/repo");

        var code = cmd.Execute(args.AsView());
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();

        Assert.Equal(0, code);
        Assert.True(Directory.Exists(Path.Combine(repo, ".ai", "repositories", "gitlab.com▸owner▸repo", ".git")));
    }

    [Fact]
    public void RejectsFourSegmentSpec()
    {
        using var tmp = new TempDir();
        var repo = MakeGitRepoWithAgent(tmp.Path);
        var cmd = new InitCommand(V(repo));
        var args = Args("a/b/c/d");

        var code = cmd.Execute(args.AsView());
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();

        Assert.Equal(1, code);
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
        var cmd = new InitCommand(V(foreign)) { CloneUrlOverride = NativeString.From(V(remoteUrl)) };
        var args = Args("-p", repo, "owner/repo");

        var code = cmd.Execute(args.AsView());
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();

        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(repo, ".ai", "config.toml")));
    }
}
