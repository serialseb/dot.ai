using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class ConfigStoreTests
{
    [Fact]
    public void LoadReturnsEmptyWhenFileMissing()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.jsonc");

        var config = ConfigStore.Load(path);

        Assert.Empty(config);
    }

    [Fact]
    public void SaveWritesJsonAndLoadReadsItBack()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.jsonc");
        var config = ConfigStore.Load(path);

        ConfigStore.AddRepo(config, "https://github.com/foo/bar");
        ConfigStore.Save(path, config);
        var reloaded = ConfigStore.Load(path);

        Assert.Contains("https://github.com/foo/bar", reloaded.Keys);
    }

    [Fact]
    public void AddRepoIsIdempotent()
    {
        var config = new Dictionary<string, System.Text.Json.JsonElement>();

        ConfigStore.AddRepo(config, "https://github.com/foo/bar");
        ConfigStore.AddRepo(config, "https://github.com/foo/bar");

        Assert.Single(config);
    }

    [Fact]
    public void AddRepoSupportsMultiple()
    {
        var config = new Dictionary<string, System.Text.Json.JsonElement>();

        ConfigStore.AddRepo(config, "https://github.com/foo/bar");
        ConfigStore.AddRepo(config, "https://github.com/baz/qux");

        Assert.Equal(2, config.Count);
    }

    [Fact]
    public void LoadToleratesComments()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.jsonc");
        File.WriteAllText(path, "// top comment\n{\n  \"https://github.com/foo/bar\": {} // inline\n}\n");

        var config = ConfigStore.Load(path);

        Assert.Contains("https://github.com/foo/bar", config.Keys);
    }

    [Fact]
    public void LoadThrowsOnNonObjectRoot()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.jsonc");
        File.WriteAllText(path, "[\"not an object\"]");

        Assert.Throws<InvalidDataException>(() => ConfigStore.Load(path));
    }

    [Fact]
    public void SaveCreatesParentDirectories()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "a", "b", "config.jsonc");
        var config = new Dictionary<string, System.Text.Json.JsonElement>();
        ConfigStore.AddRepo(config, "https://github.com/foo/bar");

        ConfigStore.Save(path, config);

        Assert.True(File.Exists(path));
    }
}
