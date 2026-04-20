using System.Text;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Dotai.Text;
using Xunit;

namespace Dotai.Tests;

public class AgentDetectorTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void ReturnsEmptyWhenNoAgents()
    {
        using var tmp = new TempDir();

        var agents = AgentDetector.Detect((FastString)B(tmp.Path));

        Assert.Empty(agents);
    }

    [Fact]
    public void DetectsClaude()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".claude"));

        var agents = AgentDetector.Detect((FastString)B(tmp.Path));

        Assert.Single(agents);
        Assert.Equal(".claude"u8.ToArray(), agents[0]);
    }

    [Fact]
    public void DetectsAllThree()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".claude"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".codex"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".opencode"));

        var agents = AgentDetector.Detect((FastString)B(tmp.Path));

        Assert.Equal(new[] { ".claude"u8.ToArray(), ".codex"u8.ToArray(), ".opencode"u8.ToArray() },
            agents, ByteArrayComparer.Instance);
    }

    [Fact]
    public void IgnoresFilesNamedLikeAgents()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, ".claude"), "");

        var agents = AgentDetector.Detect((FastString)B(tmp.Path));

        Assert.Empty(agents);
    }

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static readonly ByteArrayComparer Instance = new();
        public bool Equals(byte[]? x, byte[]? y) => x.AsSpan().SequenceEqual(y);
        public int GetHashCode(byte[] obj) => obj.Length;
    }
}
