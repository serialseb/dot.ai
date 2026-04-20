using System.Text;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Dotai.Text;
using Xunit;

namespace Dotai.Tests;

public class ConfigStoreTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    private static bool ContainsUrl(List<byte[]> config, string url)
    {
        var target = B(url);
        foreach (var item in config)
            if (item.AsSpan().SequenceEqual(target)) return true;
        return false;
    }

    [Fact]
    public void LoadReturnsEmptyWhenFileMissing()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.jsonc");

        var ok = ConfigStore.TryLoad((FastString)B(path), out var config);

        Assert.True(ok);
        Assert.Empty(config);
    }

    [Fact]
    public void SaveWritesJsonAndLoadReadsItBack()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.jsonc");
        ConfigStore.TryLoad((FastString)B(path), out var config);

        ConfigStore.AddRepo(config, (FastString)"https://github.com/foo/bar"u8);
        ConfigStore.Save((FastString)B(path), config);
        ConfigStore.TryLoad((FastString)B(path), out var reloaded);

        Assert.True(ContainsUrl(reloaded, "https://github.com/foo/bar"));
    }

    [Fact]
    public void AddRepoIsIdempotent()
    {
        var config = new List<byte[]>();

        ConfigStore.AddRepo(config, (FastString)"https://github.com/foo/bar"u8);
        ConfigStore.AddRepo(config, (FastString)"https://github.com/foo/bar"u8);

        Assert.Single(config);
    }

    [Fact]
    public void AddRepoSupportsMultiple()
    {
        var config = new List<byte[]>();

        ConfigStore.AddRepo(config, (FastString)"https://github.com/foo/bar"u8);
        ConfigStore.AddRepo(config, (FastString)"https://github.com/baz/qux"u8);

        Assert.Equal(2, config.Count);
    }

    [Fact]
    public void LoadToleratesComments()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.jsonc");
        File.WriteAllText(path, "// top comment\n{\n  \"https://github.com/foo/bar\": {} // inline\n}\n");

        var ok = ConfigStore.TryLoad((FastString)B(path), out var config);

        Assert.True(ok);
        Assert.True(ContainsUrl(config, "https://github.com/foo/bar"));
    }

    [Fact]
    public void LoadReturnsFalseOnNonObjectRoot()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.jsonc");
        File.WriteAllText(path, "[\"not an object\"]");

        var ok = ConfigStore.TryLoad((FastString)B(path), out var config);

        Assert.False(ok);
        Assert.Empty(config);
    }

    [Fact]
    public void SaveCreatesParentDirectories()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "a", "b", "config.jsonc");
        var config = new List<byte[]>();
        ConfigStore.AddRepo(config, (FastString)"https://github.com/foo/bar"u8);

        ConfigStore.Save((FastString)B(path), config);

        Assert.True(File.Exists(path));
    }
}
