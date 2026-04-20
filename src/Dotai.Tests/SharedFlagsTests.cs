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
        var r = SharedFlags.Parse(Args("owner/repo"), (FastString)B("/default"));

        Assert.Equal(B("/default"), r.StartDir);
        Assert.Equal(new[] { A("owner/repo").Data }, r.Positional.Select(a => a.Data).ToArray(), ArraySegmentComparer.Instance);
    }

    [Fact]
    public void DashPConsumesPathAndStrips()
    {
        var r = SharedFlags.Parse(Args("-p", "/tmp/foo", "owner/repo"), (FastString)B("/default"));

        Assert.Equal(B("/tmp/foo"), r.StartDir);
        Assert.Equal(new[] { A("owner/repo").Data }, r.Positional.Select(a => a.Data).ToArray(), ArraySegmentComparer.Instance);
    }

    [Fact]
    public void LongFormProjectIsEquivalent()
    {
        var r = SharedFlags.Parse(Args("--project", "/tmp/foo", "owner/repo"), (FastString)B("/default"));

        Assert.Equal(B("/tmp/foo"), r.StartDir);
    }

    [Fact]
    public void DashPWithoutValueThrows()
    {
        Assert.Throws<ArgumentException>(() => SharedFlags.Parse(Args("-p"), (FastString)B("/default")));
    }

    [Fact]
    public void UnknownFlagThrows()
    {
        Assert.Throws<ArgumentException>(() => SharedFlags.Parse(Args("--foo"), (FastString)B("/default")));
    }

    [Fact]
    public void HelpFlagIsPreservedAsPositional()
    {
        var r = SharedFlags.Parse(Args("--help"), (FastString)B("/default"));

        Assert.Contains(r.Positional, a => a.AsFast.Equals((FastString)"--help"u8));
    }

    [Fact]
    public void RelativePathIsResolvedAgainstCwd()
    {
        var cwd = Encoding.UTF8.GetBytes(Directory.GetCurrentDirectory());
        var r = SharedFlags.Parse(Args("-p", "."), (FastString)B("/default"));

        Assert.Equal(cwd, r.StartDir);
    }

    [Fact]
    public void DashFSetsForce()
    {
        var r = SharedFlags.Parse(Args("-f", "owner/repo"), (FastString)B("/default"));
        Assert.True(r.Force);
    }

    [Fact]
    public void LongFormForceIsEquivalent()
    {
        var r = SharedFlags.Parse(Args("--force", "owner/repo"), (FastString)B("/default"));
        Assert.True(r.Force);
    }

    [Fact]
    public void NoForceDefaultsFalse()
    {
        var r = SharedFlags.Parse(Args("owner/repo"), (FastString)B("/default"));
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
