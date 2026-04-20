using System.Text;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class GitClientTests
{
    private static bool IsBlankBytes(byte[] b)
    {
        foreach (var x in b)
            if (x != (byte)' ' && x != (byte)'\t' && x != (byte)'\r' && x != (byte)'\n')
                return false;
        return true;
    }

    [Fact]
    public void CloneCreatesRepository()
    {
        using var tmp = new TempDir();
        var (remote, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "hi"));
        var target = Path.Combine(tmp.Path, "clone");

        var r = GitClient.Clone(remote, target);

        Assert.Equal(0, r.ExitCode);
        Assert.True(Directory.Exists(Path.Combine(target, ".git")));
    }

    [Fact]
    public void StatusPorcelainReturnsEmptyForCleanRepo()
    {
        using var tmp = new TempDir();
        var (_, work) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "hi"));

        var r = GitClient.StatusPorcelain(work);

        Assert.Equal(0, r.ExitCode);
        Assert.True(IsBlankBytes(r.StdOut));
    }

    [Fact]
    public void StatusPorcelainReportsDirtyRepo()
    {
        using var tmp = new TempDir();
        var (_, work) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "hi"));
        File.WriteAllText(Path.Combine(work, "readme.md"), "changed");

        var r = GitClient.StatusPorcelain(work);

        Assert.False(IsBlankBytes(r.StdOut));
    }

    [Fact]
    public void DefaultBranchReturnsMain()
    {
        using var tmp = new TempDir();
        var (_, work) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "hi"));

        var branch = GitClient.DefaultBranch(work);

        Assert.Equal("main"u8.ToArray(), branch);
    }

    [Fact]
    public void RebaseInProgressDetectsAbsence()
    {
        using var tmp = new TempDir();
        var (_, work) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "hi"));

        Assert.False(GitClient.RebaseInProgress(work));
    }

    [Fact]
    public void RebaseInProgressDetectsPresence()
    {
        using var tmp = new TempDir();
        var (_, work) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "hi"));
        Directory.CreateDirectory(Path.Combine(work, ".git", "rebase-merge"));

        Assert.True(GitClient.RebaseInProgress(work));
    }
}
