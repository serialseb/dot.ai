using System.Text;
using Dotai.Native;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class ConfigStoreTests
{
    private static NativeStringView V(string s) => Encoding.UTF8.GetBytes(s);
    private static NativeStringView V(ReadOnlySpan<byte> s) => s;

    private static bool ContainsName(NativeList<RepoConfig> config, string name)
    {
        NativeStringView target = Encoding.UTF8.GetBytes(name);
        for (int i = 0; i < config.Length; i++)
            if (config[i].Name.AsView() == target) return true;
        return false;
    }

    [Fact]
    public void LoadReturnsEmptyWhenFileMissing()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.toml");

        var ok = ConfigStore.TryLoad(V(path), out var config);

        Assert.True(ok);
        Assert.Equal(0, config.Length);
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();
    }

    [Fact]
    public void SaveWritesTomlAndLoadReadsItBack()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.toml");
        ConfigStore.TryLoad(V(path), out var config);

        ConfigStore.AddRepo(ref config, "foo/bar"u8, "merge"u8);
        ConfigStore.Save(V(path), config.AsView());
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();

        ConfigStore.TryLoad(V(path), out var reloaded);

        Assert.True(ContainsName(reloaded, "foo/bar"));
        for (int i = 0; i < reloaded.Length; i++) reloaded[i].Dispose();
        reloaded.Dispose();
    }

    [Fact]
    public void AddRepoIsIdempotent()
    {
        var config = new NativeList<RepoConfig>(4);

        ConfigStore.AddRepo(ref config, "foo/bar"u8, "merge"u8);
        ConfigStore.AddRepo(ref config, "foo/bar"u8, "merge"u8);

        Assert.Equal(1, config.Length);
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();
    }

    [Fact]
    public void AddRepoSupportsMultiple()
    {
        var config = new NativeList<RepoConfig>(4);

        ConfigStore.AddRepo(ref config, "foo/bar"u8, "merge"u8);
        ConfigStore.AddRepo(ref config, "baz/qux"u8, "merge"u8);

        Assert.Equal(2, config.Length);
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();
    }

    [Fact]
    public void LoadToleratesCommentsAndBlankLines()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.toml");
        File.WriteAllText(path,
            "# dotai sources\n\n[\"foo/bar\"]\nmode = \"merge\"\n\n# trailing comment\n");

        var ok = ConfigStore.TryLoad(V(path), out var config);

        Assert.True(ok);
        Assert.True(ContainsName(config, "foo/bar"));
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();
    }

    [Fact]
    public void LoadReturnsFalseOnUnclosedSectionHeader()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.toml");
        File.WriteAllText(path, "[\"foo/bar\"\nmode = \"merge\"\n");

        var ok = ConfigStore.TryLoad(V(path), out var config);

        Assert.False(ok);
        Assert.Equal(0, config.Length);
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();
    }

    [Fact]
    public void SaveCreatesParentDirectories()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "a", "b", "config.toml");
        var config = new NativeList<RepoConfig>(4);
        ConfigStore.AddRepo(ref config, "foo/bar"u8, "merge"u8);

        ConfigStore.Save(V(path), config.AsView());

        Assert.True(File.Exists(path));
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();
    }

    [Fact]
    public void ContainsReturnsTrueForExistingName()
    {
        var config = new NativeList<RepoConfig>(4);
        ConfigStore.AddRepo(ref config, "foo/bar"u8, "merge"u8);

        Assert.True(ConfigStore.Contains(config.AsView(), "foo/bar"u8));
        Assert.False(ConfigStore.Contains(config.AsView(), "baz/qux"u8));

        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();
    }
}
