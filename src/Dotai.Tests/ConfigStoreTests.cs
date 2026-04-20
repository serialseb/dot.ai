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

    private static bool ContainsUrl(NativeList<NativeString> config, string url)
    {
        NativeStringView target = Encoding.UTF8.GetBytes(url);
        for (int i = 0; i < config.Length; i++)
            if (config[i].AsView() == target) return true;
        return false;
    }

    [Fact]
    public void LoadReturnsEmptyWhenFileMissing()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.jsonc");

        var ok = ConfigStore.TryLoad(V(path), out var config);

        Assert.True(ok);
        Assert.Equal(0, config.Length);
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();
    }

    [Fact]
    public void SaveWritesJsonAndLoadReadsItBack()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.jsonc");
        ConfigStore.TryLoad(V(path), out var config);

        ConfigStore.AddRepo(ref config, "https://github.com/foo/bar"u8);
        ConfigStore.Save(V(path), config);
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();

        ConfigStore.TryLoad(V(path), out var reloaded);

        Assert.True(ContainsUrl(reloaded, "https://github.com/foo/bar"));
        for (int i = 0; i < reloaded.Length; i++) reloaded[i].Dispose();
        reloaded.Dispose();
    }

    [Fact]
    public void AddRepoIsIdempotent()
    {
        var config = new NativeList<NativeString>(4);

        ConfigStore.AddRepo(ref config, "https://github.com/foo/bar"u8);
        ConfigStore.AddRepo(ref config, "https://github.com/foo/bar"u8);

        Assert.Equal(1, config.Length);
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();
    }

    [Fact]
    public void AddRepoSupportsMultiple()
    {
        var config = new NativeList<NativeString>(4);

        ConfigStore.AddRepo(ref config, "https://github.com/foo/bar"u8);
        ConfigStore.AddRepo(ref config, "https://github.com/baz/qux"u8);

        Assert.Equal(2, config.Length);
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();
    }

    [Fact]
    public void LoadToleratesComments()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.jsonc");
        File.WriteAllText(path, "// top comment\n{\n  \"https://github.com/foo/bar\": {} // inline\n}\n");

        var ok = ConfigStore.TryLoad(V(path), out var config);

        Assert.True(ok);
        Assert.True(ContainsUrl(config, "https://github.com/foo/bar"));
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();
    }

    [Fact]
    public void LoadReturnsFalseOnNonObjectRoot()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "config.jsonc");
        File.WriteAllText(path, "[\"not an object\"]");

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
        var path = Path.Combine(tmp.Path, "a", "b", "config.jsonc");
        var config = new NativeList<NativeString>(4);
        ConfigStore.AddRepo(ref config, "https://github.com/foo/bar"u8);

        ConfigStore.Save(V(path), config);

        Assert.True(File.Exists(path));
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();
    }
}
