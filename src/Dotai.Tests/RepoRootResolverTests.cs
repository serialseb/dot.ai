using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class RepoRootResolverTests
{
    [Fact]
    public void FindsGitRootFromRootItself()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".git"));

        var root = RepoRootResolver.Find(tmp.Path);

        Assert.Equal(tmp.Path, root);
    }

    [Fact]
    public void FindsGitRootFromSubdirectory()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".git"));
        var sub = Path.Combine(tmp.Path, "a", "b", "c");
        Directory.CreateDirectory(sub);

        var root = RepoRootResolver.Find(sub);

        Assert.Equal(tmp.Path, root);
    }

    [Fact]
    public void ReturnsNullWhenNoGitRoot()
    {
        using var tmp = new TempDir();

        var root = RepoRootResolver.Find(tmp.Path);

        Assert.Null(root);
    }

    [Fact]
    public void ReturnsNullForNonExistentDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dotai-tests-" + Guid.NewGuid().ToString("N"));
        // deliberately NOT created
        var root = RepoRootResolver.Find(path);
        Assert.Null(root);
    }

    [Fact]
    public void TreatsDotGitFileAsRoot()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, ".git"), "gitdir: /elsewhere");

        var root = RepoRootResolver.Find(tmp.Path);

        Assert.Equal(tmp.Path, root);
    }
}
