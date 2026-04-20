using System.Text;
using Dotai.Commands;
using Dotai.Native;
using Xunit;

namespace Dotai.Tests;

public class SharedFlagsTests
{
    private static NativeStringView V(string s) => Encoding.UTF8.GetBytes(s);

    private static NativeList<NativeString> Args(params string[] ss)
    {
        var r = new NativeList<NativeString>(ss.Length > 0 ? ss.Length : 1);
        for (int i = 0; i < ss.Length; i++) r.Add(NativeString.From(V(ss[i])));
        return r;
    }

    [Fact]
    public void NoFlagsReturnsDefaultStartDirAndPositional()
    {
        var args = Args("owner/repo");
        var ok = SharedFlags.TryParse(args.AsView(), V("/default"), out var r);

        Assert.True(ok);
        Assert.True(r.StartDir.AsView() == V("/default"));
        Assert.Equal(1, r.Positional.Length);
        Assert.True(r.Positional[0].AsView() == V("owner/repo"));

        r.Dispose();
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();
    }

    [Fact]
    public void DashPConsumesPathAndStrips()
    {
        var args = Args("-p", "/tmp/foo", "owner/repo");
        var ok = SharedFlags.TryParse(args.AsView(), V("/default"), out var r);

        Assert.True(ok);
        Assert.True(r.StartDir.AsView() == V("/tmp/foo"));
        Assert.Equal(1, r.Positional.Length);
        Assert.True(r.Positional[0].AsView() == V("owner/repo"));

        r.Dispose();
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();
    }

    [Fact]
    public void LongFormProjectIsEquivalent()
    {
        var args = Args("--project", "/tmp/foo", "owner/repo");
        var ok = SharedFlags.TryParse(args.AsView(), V("/default"), out var r);

        Assert.True(ok);
        Assert.True(r.StartDir.AsView() == V("/tmp/foo"));

        r.Dispose();
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();
    }

    [Fact]
    public void DashPWithoutValueReturnsFalse()
    {
        var args = Args("-p");
        var ok = SharedFlags.TryParse(args.AsView(), V("/default"), out _);
        Assert.False(ok);
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();
    }

    [Fact]
    public void UnknownFlagReturnsFalse()
    {
        var args = Args("--foo");
        var ok = SharedFlags.TryParse(args.AsView(), V("/default"), out _);
        Assert.False(ok);
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();
    }

    [Fact]
    public void HelpFlagIsPreservedAsPositional()
    {
        var args = Args("--help");
        var ok = SharedFlags.TryParse(args.AsView(), V("/default"), out var r);

        Assert.True(ok);
        bool found = false;
        for (int i = 0; i < r.Positional.Length; i++)
            if (r.Positional[i].AsView() == "--help"u8) { found = true; break; }
        Assert.True(found);

        r.Dispose();
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();
    }

    [Fact]
    public void RelativePathIsResolvedAgainstCwd()
    {
        var cwd = Encoding.UTF8.GetBytes(Directory.GetCurrentDirectory());
        var args = Args("-p", ".");
        var ok = SharedFlags.TryParse(args.AsView(), V("/default"), out var r);

        Assert.True(ok);
        Assert.True(r.StartDir.AsView() == (NativeStringView)cwd);

        r.Dispose();
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();
    }

    [Fact]
    public void DashFSetsForce()
    {
        var args = Args("-f", "owner/repo");
        var ok = SharedFlags.TryParse(args.AsView(), V("/default"), out var r);
        Assert.True(ok);
        Assert.True(r.Force);
        r.Dispose();
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();
    }

    [Fact]
    public void LongFormForceIsEquivalent()
    {
        var args = Args("--force", "owner/repo");
        var ok = SharedFlags.TryParse(args.AsView(), V("/default"), out var r);
        Assert.True(ok);
        Assert.True(r.Force);
        r.Dispose();
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();
    }

    [Fact]
    public void NoForceDefaultsFalse()
    {
        var args = Args("owner/repo");
        var ok = SharedFlags.TryParse(args.AsView(), V("/default"), out var r);
        Assert.True(ok);
        Assert.False(r.Force);
        r.Dispose();
        for (int i = 0; i < args.Length; i++) args[i].Dispose();
        args.Dispose();
    }
}
