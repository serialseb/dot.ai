namespace Dotai.Services;

public static class AgentDetector
{
    private static readonly string[] Known = { ".claude", ".codex", ".opencode" };

    public static IReadOnlyList<string> Detect(string repoRoot)
    {
        var present = new List<string>();
        foreach (var name in Known)
        {
            if (Directory.Exists(Path.Combine(repoRoot, name)))
                present.Add(name);
        }
        return present;
    }
}
