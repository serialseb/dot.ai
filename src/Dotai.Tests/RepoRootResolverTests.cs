using System.Text;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Dotai.Text;
using Xunit;

namespace Dotai.Tests;

public class RepoRootResolverTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void FindsGitRootFromRootItself()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".git"));

        var found = RepoRootResolver.TryFind((FastString)B(tmp.Path), out var root);

        Assert.True(found);
        Assert.Equal(B(tmp.Path), root);
    }

    [Fact]
    public void FindsGitRootFromSubdirectory()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".git"));
        var sub = Path.Combine(tmp.Path, "a", "b", "c");
        Directory.CreateDirectory(sub);

        var found = RepoRootResolver.TryFind((FastString)B(sub), out var root);

        Assert.True(found);
        Assert.Equal(B(tmp.Path), root);
    }

    [Fact]
    public void ReturnsNullWhenNoGitRoot()
    {
        using var tmp = new TempDir();

        var found = RepoRootResolver.TryFind((FastString)B(tmp.Path), out _);

        Assert.False(found);
    }

    [Fact]
    public void ReturnsNullForNonExistentDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dotai-tests-" + Guid.NewGuid().ToString("N"));
        // deliberately NOT created
        var found = RepoRootResolver.TryFind((FastString)B(path), out _);
        Assert.False(found);
    }

    [Fact]
    public void TreatsDotGitFileAsRoot()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, ".git"), "gitdir: /elsewhere");

        var found = RepoRootResolver.TryFind((FastString)B(tmp.Path), out var root);

        Assert.True(found);
        Assert.Equal(B(tmp.Path), root);
    }
}
