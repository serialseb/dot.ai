using System.Text;
using Dotai.Native;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class GitClientTests
{
    private static NativeStringView V(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void CloneCreatesRepository()
    {
        using var tmp = new TempDir();
        var (remote, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "hi"));
        var target = Path.Combine(tmp.Path, "clone");

        var r = GitClient.Clone(V(remote), V(target));

        Assert.Equal(0, r.ExitCode);
        Assert.True(Directory.Exists(Path.Combine(target, ".git")));
        r.Dispose();
    }

    [Fact]
    public void StatusPorcelainReturnsEmptyForCleanRepo()
    {
        using var tmp = new TempDir();
        var (_, work) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "hi"));

        var r = GitClient.StatusPorcelain(V(work));

        Assert.Equal(0, r.ExitCode);
        Assert.True(r.StdOut.AsView().IsBlank());
        r.Dispose();
    }

    [Fact]
    public void StatusPorcelainReportsDirtyRepo()
    {
        using var tmp = new TempDir();
        var (_, work) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "hi"));
        File.WriteAllText(Path.Combine(work, "readme.md"), "changed");

        var r = GitClient.StatusPorcelain(V(work));

        Assert.False(r.StdOut.AsView().IsBlank());
        r.Dispose();
    }

    [Fact]
    public void DefaultBranchReturnsMain()
    {
        using var tmp = new TempDir();
        var (_, work) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "hi"));

        var branch = GitClient.DefaultBranch(V(work));

        Assert.True(branch.AsView() == "main"u8);
        branch.Dispose();
    }

    [Fact]
    public void RebaseInProgressDetectsAbsence()
    {
        using var tmp = new TempDir();
        var (_, work) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "hi"));

        Assert.False(GitClient.RebaseInProgress(V(work)));
    }

    [Fact]
    public void RebaseInProgressDetectsPresence()
    {
        using var tmp = new TempDir();
        var (_, work) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "hi"));
        Directory.CreateDirectory(Path.Combine(work, ".git", "rebase-merge"));

        Assert.True(GitClient.RebaseInProgress(V(work)));
    }

    [Fact]
    public void BuildCloneUrlDefaultsToGithubForTwoSegmentSpec()
    {
        var url = GitClient.BuildCloneUrl(V("owner/repo"));
        Assert.True(url.AsView() == "https://github.com/owner/repo"u8);
        url.Dispose();
    }

    [Fact]
    public void BuildCloneUrlUsesHostForThreeSegmentSpec()
    {
        var url = GitClient.BuildCloneUrl(V("gitlab.com/owner/repo"));
        Assert.True(url.AsView() == "https://gitlab.com/owner/repo"u8);
        url.Dispose();
    }

    [Fact]
    public void BuildCloneUrlPreservesNonDefaultPort()
    {
        var url = GitClient.BuildCloneUrl(V("git.local:8443/owner/repo"));
        Assert.True(url.AsView() == "https://git.local:8443/owner/repo"u8);
        url.Dispose();
    }

    [Fact]
    public void BuildCloneUrlStripsDefaultHttpsPort()
    {
        var url = GitClient.BuildCloneUrl(V("git.local:443/owner/repo"));
        Assert.True(url.AsView() == "https://git.local/owner/repo"u8);
        url.Dispose();
    }

    [Fact]
    public void DeriveCloneNameUsesTriangleSeparator()
    {
        var name = GitClient.DeriveCloneName(V("gitlab.com/owner/repo"));
        Assert.True(name.AsView() == "gitlab.com▸owner▸repo"u8);
        name.Dispose();
    }

    [Fact]
    public void DeriveCloneNameStripsPortEntirely()
    {
        var name = GitClient.DeriveCloneName(V("git.local:8443/owner/repo"));
        Assert.True(name.AsView() == "git.local▸owner▸repo"u8);
        name.Dispose();
    }

    [Fact]
    public void DeriveCloneNameDefaultsLegacySpecToGithubHost()
    {
        var name = GitClient.DeriveCloneName(V("owner/repo"));
        Assert.True(name.AsView() == "github.com▸owner▸repo"u8);
        name.Dispose();
    }
}
