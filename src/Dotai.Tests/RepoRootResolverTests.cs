using System.Text;
using Dotai.Native;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class RepoRootResolverTests
{
    private static NativeStringView V(string s) => Encoding.UTF8.GetBytes(s);
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void FindsGitRootFromRootItself()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".git"));

        var found = RepoRootResolver.TryFind(V(tmp.Path), out var root);

        Assert.True(found);
        Assert.True(root.AsView() == V(tmp.Path));
        root.Dispose();
    }

    [Fact]
    public void FindsGitRootFromSubdirectory()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".git"));
        var sub = Path.Combine(tmp.Path, "a", "b", "c");
        Directory.CreateDirectory(sub);

        var found = RepoRootResolver.TryFind(V(sub), out var root);

        Assert.True(found);
        Assert.True(root.AsView() == V(tmp.Path));
        root.Dispose();
    }

    [Fact]
    public void ReturnsNullWhenNoGitRoot()
    {
        using var tmp = new TempDir();

        var found = RepoRootResolver.TryFind(V(tmp.Path), out var root);

        Assert.False(found);
        root.Dispose();
    }

    [Fact]
    public void ReturnsNullForNonExistentDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dotai-tests-" + Guid.NewGuid().ToString("N"));
        var found = RepoRootResolver.TryFind(V(path), out var root);
        Assert.False(found);
        root.Dispose();
    }

    [Fact]
    public void TreatsDotGitFileAsRoot()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, ".git"), "gitdir: /elsewhere");

        var found = RepoRootResolver.TryFind(V(tmp.Path), out var root);

        Assert.True(found);
        Assert.True(root.AsView() == V(tmp.Path));
        root.Dispose();
    }
}
