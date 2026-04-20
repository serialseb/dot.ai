using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class AgentDetectorTests
{
    [Fact]
    public void ReturnsEmptyWhenNoAgents()
    {
        using var tmp = new TempDir();

        var agents = AgentDetector.Detect(tmp.Path);

        Assert.Empty(agents);
    }

    [Fact]
    public void DetectsClaude()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".claude"));

        var agents = AgentDetector.Detect(tmp.Path);

        Assert.Single(agents);
        Assert.Equal(".claude", agents[0]);
    }

    [Fact]
    public void DetectsAllThree()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".claude"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".codex"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".opencode"));

        var agents = AgentDetector.Detect(tmp.Path);

        Assert.Equal(new[] { ".claude", ".codex", ".opencode" }, agents.ToArray());
    }

    [Fact]
    public void IgnoresFilesNamedLikeAgents()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, ".claude"), "");

        var agents = AgentDetector.Detect(tmp.Path);

        Assert.Empty(agents);
    }
}
