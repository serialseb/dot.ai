using System.Text;
using Dotai.Native;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class AgentDetectorTests
{
    private static NativeStringView V(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void ReturnsEmptyWhenNoAgents()
    {
        using var tmp = new TempDir();

        var agents = AgentDetector.Detect(V(tmp.Path));

        Assert.Equal(0, agents.Length);
        for (int i = 0; i < agents.Length; i++) agents[i].Dispose();
        agents.Dispose();
    }

    [Fact]
    public void DetectsClaude()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".claude"));

        var agents = AgentDetector.Detect(V(tmp.Path));

        Assert.Equal(1, agents.Length);
        Assert.True(agents[0].AsView() == ".claude"u8);
        for (int i = 0; i < agents.Length; i++) agents[i].Dispose();
        agents.Dispose();
    }

    [Fact]
    public void DetectsAllThree()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".claude"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".codex"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".opencode"));

        var agents = AgentDetector.Detect(V(tmp.Path));

        Assert.Equal(3, agents.Length);
        Assert.True(agents[0].AsView() == ".claude"u8);
        Assert.True(agents[1].AsView() == ".codex"u8);
        Assert.True(agents[2].AsView() == ".opencode"u8);
        for (int i = 0; i < agents.Length; i++) agents[i].Dispose();
        agents.Dispose();
    }

    [Fact]
    public void IgnoresFilesNamedLikeAgents()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, ".claude"), "");

        var agents = AgentDetector.Detect(V(tmp.Path));

        Assert.Equal(0, agents.Length);
        for (int i = 0; i < agents.Length; i++) agents[i].Dispose();
        agents.Dispose();
    }
}
