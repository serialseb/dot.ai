using System.Text;
using Dotai.Native;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class GitignoreWriterTests
{
    private static NativeStringView V(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void CreatesFileWhenMissing()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, ".gitignore");

        GitignoreWriter.EnsureLine(V(path), "repositories/"u8);

        Assert.Contains("repositories/", File.ReadAllText(path));
    }

    [Fact]
    public void AppendsLineWhenMissing()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, ".gitignore");
        File.WriteAllText(path, "node_modules/\n");

        GitignoreWriter.EnsureLine(V(path), "repositories/"u8);

        var text = File.ReadAllText(path);
        Assert.Contains("node_modules/", text);
        Assert.Contains("repositories/", text);
    }

    [Fact]
    public void IsIdempotent()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, ".gitignore");
        File.WriteAllText(path, "repositories/\n");

        GitignoreWriter.EnsureLine(V(path), "repositories/"u8);
        GitignoreWriter.EnsureLine(V(path), "repositories/"u8);

        var count = File.ReadAllText(path).Split("repositories/").Length - 1;
        Assert.Equal(1, count);
    }

    [Fact]
    public void CreatesParentDirectoriesIfMissing()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "a", "b", ".gitignore");

        GitignoreWriter.EnsureLine(V(path), "repositories/"u8);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void AppendsToFileWithoutFinalNewline()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, ".gitignore");
        File.WriteAllText(path, "node_modules/");

        GitignoreWriter.EnsureLine(V(path), "repositories/"u8);

        var text = File.ReadAllText(path);
        Assert.Contains("node_modules/", text);
        Assert.Contains("repositories/", text);
        Assert.EndsWith(Environment.NewLine, text);
    }
}
