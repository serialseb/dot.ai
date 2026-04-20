using Dotai.Native;
using Xunit;

namespace Dotai.Tests.Text;

public class NativeStringViewTests
{
    [Fact]
    public void StartsWithReturnsTrueForMatchingPrefix()
    {
        NativeStringView s = "hello world"u8;
        Assert.True(s.StartsWith("hello"u8));
    }

    [Fact]
    public void StartsWithReturnsFalseForNonMatchingPrefix()
    {
        NativeStringView s = "hello world"u8;
        Assert.False(s.StartsWith("world"u8));
    }

    [Fact]
    public void EndsWithReturnsTrueForMatchingSuffix()
    {
        NativeStringView s = "hello.git"u8;
        Assert.True(s.EndsWith(".git"u8));
    }

    [Fact]
    public void EndsWithReturnsFalseForNonMatchingSuffix()
    {
        NativeStringView s = "hello.git"u8;
        Assert.False(s.EndsWith(".svn"u8));
    }

    [Fact]
    public void EqualsReturnsTrueForIdenticalContent()
    {
        NativeStringView a = "https://github.com/foo/bar"u8;
        NativeStringView b = "https://github.com/foo/bar"u8;
        Assert.True(a == b);
    }

    [Fact]
    public void EqualsReturnsFalseForDifferentContent()
    {
        NativeStringView a = "https://github.com/foo/bar"u8;
        NativeStringView b = "https://github.com/foo/baz"u8;
        Assert.False(a == b);
    }

    [Fact]
    public void IndexOfFindsFirstOccurrence()
    {
        NativeStringView s = "origin/main"u8;
        Assert.Equal(6, s.IndexOf((byte)'/'));
    }

    [Fact]
    public void LastIndexOfFindsLastOccurrence()
    {
        NativeStringView s = "https://github.com/foo/bar"u8;
        Assert.Equal(22, s.LastIndexOf((byte)'/'));
    }

    [Fact]
    public void TrimRemovesLeadingAndTrailingWhitespace()
    {
        NativeStringView s = "  main\n"u8;
        NativeStringView trimmed = s.Trim();
        Assert.True(trimmed == "main"u8);
    }
}
