using System.Text;
using Dotai.Commands;
using Dotai.Text;
using Xunit;

namespace Dotai.Tests;

public class SharedFlagsTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);
    private static Arg A(string s) => new Arg(B(s));
    private static Arg[] Args(params string[] ss) { var r = new Arg[ss.Length]; for (int i = 0; i < ss.Length; i++) r[i] = A(ss[i]); return r; }

    [Fact]
    public void NoFlagsReturnsDefaultStartDirAndPositional()
    {
        var ok = SharedFlags.TryParse(Args("owner/repo"), (FastString)B("/default"), out var r);

        Assert.True(ok);
        Assert.Equal(B("/default"), r.StartDir);
        Assert.Equal(new[] { A("owner/repo").Data }, r.Positional.Select(a => a.Data).ToArray(), ArraySegmentComparer.Instance);
    }

    [Fact]
    public void DashPConsumesPathAndStrips()
    {
        var ok = SharedFlags.TryParse(Args("-p", "/tmp/foo", "owner/repo"), (FastString)B("/default"), out var r);

        Assert.True(ok);
        Assert.Equal(B("/tmp/foo"), r.StartDir);
        Assert.Equal(new[] { A("owner/repo").Data }, r.Positional.Select(a => a.Data).ToArray(), ArraySegmentComparer.Instance);
    }

    [Fact]
    public void LongFormProjectIsEquivalent()
    {
        var ok = SharedFlags.TryParse(Args("--project", "/tmp/foo", "owner/repo"), (FastString)B("/default"), out var r);

        Assert.True(ok);
        Assert.Equal(B("/tmp/foo"), r.StartDir);
    }

    [Fact]
    public void DashPWithoutValueReturnsFalse()
    {
        var ok = SharedFlags.TryParse(Args("-p"), (FastString)B("/default"), out _);
        Assert.False(ok);
    }

    [Fact]
    public void UnknownFlagReturnsFalse()
    {
        var ok = SharedFlags.TryParse(Args("--foo"), (FastString)B("/default"), out _);
        Assert.False(ok);
    }

    [Fact]
    public void HelpFlagIsPreservedAsPositional()
    {
        var ok = SharedFlags.TryParse(Args("--help"), (FastString)B("/default"), out var r);

        Assert.True(ok);
        Assert.Contains(r.Positional, a => a.AsFast.Equals((FastString)"--help"u8));
    }

    [Fact]
    public void RelativePathIsResolvedAgainstCwd()
    {
        var cwd = Encoding.UTF8.GetBytes(Directory.GetCurrentDirectory());
        var ok = SharedFlags.TryParse(Args("-p", "."), (FastString)B("/default"), out var r);

        Assert.True(ok);
        Assert.Equal(cwd, r.StartDir);
    }

    [Fact]
    public void DashFSetsForce()
    {
        var ok = SharedFlags.TryParse(Args("-f", "owner/repo"), (FastString)B("/default"), out var r);
        Assert.True(ok);
        Assert.True(r.Force);
    }

    [Fact]
    public void LongFormForceIsEquivalent()
    {
        var ok = SharedFlags.TryParse(Args("--force", "owner/repo"), (FastString)B("/default"), out var r);
        Assert.True(ok);
        Assert.True(r.Force);
    }

    [Fact]
    public void NoForceDefaultsFalse()
    {
        var ok = SharedFlags.TryParse(Args("owner/repo"), (FastString)B("/default"), out var r);
        Assert.True(ok);
        Assert.False(r.Force);
    }

    // Comparer for Assert.Equal on byte[][] sequences
    private sealed class ArraySegmentComparer : IEqualityComparer<byte[]>
    {
        public static readonly ArraySegmentComparer Instance = new();
        public bool Equals(byte[]? x, byte[]? y) => x.AsSpan().SequenceEqual(y);
        public int GetHashCode(byte[] obj) => obj.Length;
    }
}
