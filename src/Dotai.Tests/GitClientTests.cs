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
}
