using Dotai.Commands;
using Xunit;

namespace Dotai.Tests;

public class SharedFlagsTests
{
    [Fact]
    public void NoFlagsReturnsDefaultStartDirAndPositional()
    {
        var r = SharedFlags.Parse(new[] { "owner/repo" }, "/default");

        Assert.Equal("/default", r.StartDir);
        Assert.Equal(new[] { "owner/repo" }, r.Positional);
    }

    [Fact]
    public void DashPConsumesPathAndStrips()
    {
        var r = SharedFlags.Parse(new[] { "-p", "/tmp/foo", "owner/repo" }, "/default");

        Assert.Equal("/tmp/foo", r.StartDir);
        Assert.Equal(new[] { "owner/repo" }, r.Positional);
    }

    [Fact]
    public void LongFormProjectIsEquivalent()
    {
        var r = SharedFlags.Parse(new[] { "--project", "/tmp/foo", "owner/repo" }, "/default");

        Assert.Equal("/tmp/foo", r.StartDir);
    }

    [Fact]
    public void DashPWithoutValueThrows()
    {
        Assert.Throws<ArgumentException>(() => SharedFlags.Parse(new[] { "-p" }, "/default"));
    }

    [Fact]
    public void UnknownFlagThrows()
    {
        Assert.Throws<ArgumentException>(() => SharedFlags.Parse(new[] { "--foo" }, "/default"));
    }

    [Fact]
    public void HelpFlagIsPreservedAsPositional()
    {
        var r = SharedFlags.Parse(new[] { "--help" }, "/default");

        Assert.Contains("--help", r.Positional);
    }

    [Fact]
    public void RelativePathIsResolvedAgainstCwd()
    {
        var cwd = Directory.GetCurrentDirectory();
        var r = SharedFlags.Parse(new[] { "-p", "." }, "/default");

        Assert.Equal(cwd, r.StartDir);
    }

    [Fact]
    public void DashFSetsForce()
    {
        var r = SharedFlags.Parse(new[] { "-f", "owner/repo" }, "/default");
        Assert.True(r.Force);
    }

    [Fact]
    public void LongFormForceIsEquivalent()
    {
        var r = SharedFlags.Parse(new[] { "--force", "owner/repo" }, "/default");
        Assert.True(r.Force);
    }

    [Fact]
    public void NoForceDefaultsFalse()
    {
        var r = SharedFlags.Parse(new[] { "owner/repo" }, "/default");
        Assert.False(r.Force);
    }
}
