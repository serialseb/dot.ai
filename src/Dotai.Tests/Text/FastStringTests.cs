using Dotai.Text;
using Xunit;

namespace Dotai.Tests.Text;

public class FastStringTests
{
    [Fact]
    public void StartsWithReturnsTrueForMatchingPrefix()
    {
        FastString s = "hello world"u8;
        Assert.True(s.StartsWith("hello"u8));
    }

    [Fact]
    public void StartsWithReturnsFalseForNonMatchingPrefix()
    {
        FastString s = "hello world"u8;
        Assert.False(s.StartsWith("world"u8));
    }

    [Fact]
    public void EndsWithReturnsTrueForMatchingSuffix()
    {
        FastString s = "hello.git"u8;
        Assert.True(s.EndsWith(".git"u8));
    }

    [Fact]
    public void EndsWithReturnsFalseForNonMatchingSuffix()
    {
        FastString s = "hello.git"u8;
        Assert.False(s.EndsWith(".svn"u8));
    }

    [Fact]
    public void EqualsReturnsTrueForIdenticalContent()
    {
        FastString a = "https://github.com/foo/bar"u8;
        FastString b = "https://github.com/foo/bar"u8;
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void EqualsReturnsFalseForDifferentContent()
    {
        FastString a = "https://github.com/foo/bar"u8;
        FastString b = "https://github.com/foo/baz"u8;
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void IndexOfFindsFirstOccurrence()
    {
        FastString s = "origin/main"u8;
        Assert.Equal(6, s.IndexOf((byte)'/'));
    }

    [Fact]
    public void LastIndexOfFindsLastOccurrence()
    {
        FastString s = "https://github.com/foo/bar"u8;
        Assert.Equal(22, s.LastIndexOf((byte)'/'));
    }

    [Fact]
    public void TrimRemovesLeadingAndTrailingWhitespace()
    {
        FastString s = "  main\n"u8;
        FastString trimmed = s.Trim();
        Assert.True(trimmed.Equals("main"u8));
    }
}
