using System.Text;
using Dotai.Native;
using Dotai.Services;
using Xunit;

namespace Dotai.Tests;

public class TomlConfigTests
{
    private static NativeStringView V(string s) => Encoding.UTF8.GetBytes(s);

    private static bool ContainsEntry(NativeList<RepoConfig> list, string name, string mode)
    {
        NativeStringView n = Encoding.UTF8.GetBytes(name);
        NativeStringView m = Encoding.UTF8.GetBytes(mode);
        for (int i = 0; i < list.Length; i++)
            if (list[i].Name.AsView() == n && list[i].Mode.AsView() == m) return true;
        return false;
    }

    [Fact]
    public void ParsesWellFormedConfig()
    {
        var src = V("# dotai sources\n\n[\"foo/bar\"]\nmode = \"merge\"\n\n[\"baz/qux\"]\nmode = \"merge\"\n\n");

        var ok = TomlConfig.TryParse(src, out var result);

        Assert.True(ok);
        Assert.Equal(2, result.Length);
        Assert.True(ContainsEntry(result, "foo/bar", "merge"));
        Assert.True(ContainsEntry(result, "baz/qux", "merge"));
        for (int i = 0; i < result.Length; i++) result[i].Dispose();
        result.Dispose();
    }

    [Fact]
    public void ToleratesCommentsAndBlankLines()
    {
        var src = V("# top\n\n# another comment\n[\"owner/repo\"]\n# inline\nmode = \"merge\"\n\n");

        var ok = TomlConfig.TryParse(src, out var result);

        Assert.True(ok);
        Assert.Equal(1, result.Length);
        Assert.True(ContainsEntry(result, "owner/repo", "merge"));
        for (int i = 0; i < result.Length; i++) result[i].Dispose();
        result.Dispose();
    }

    [Fact]
    public void RejectsUnclosedSectionHeader()
    {
        var src = V("[\"foo/bar\"\nmode = \"merge\"\n");

        var ok = TomlConfig.TryParse(src, out var result);

        Assert.False(ok);
        Assert.Equal(0, result.Length);
        result.Dispose();
    }

    [Fact]
    public void DefaultsModeToMergeWhenKeyAbsent()
    {
        var src = V("[\"foo/bar\"]\n\n");

        var ok = TomlConfig.TryParse(src, out var result);

        Assert.True(ok);
        Assert.Equal(1, result.Length);
        Assert.True(ContainsEntry(result, "foo/bar", "merge"));
        for (int i = 0; i < result.Length; i++) result[i].Dispose();
        result.Dispose();
    }

    [Fact]
    public void WriteProducesExpectedToml()
    {
        var config = new NativeList<RepoConfig>(2);
        config.Add(new RepoConfig
        {
            Name = NativeString.From("foo/bar"u8),
            Mode = NativeString.From("merge"u8)
        });

        var buf = new NativeBuffer(128);
        TomlConfig.Write(config.AsView(), ref buf);
        var text = Encoding.UTF8.GetString(buf.AsView().Bytes);

        Assert.Contains("[\"foo/bar\"]", text);
        Assert.Contains("mode = \"merge\"", text);
        Assert.Contains("# dotai sources", text);

        buf.Dispose();
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();
    }
}
