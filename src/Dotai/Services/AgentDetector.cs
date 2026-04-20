namespace Dotai.Services;

public static class AgentDetector
{
    private static readonly string[] Known = { ".claude", ".codex", ".opencode" };

    public static string[] Detect(string repoRoot)
    {
        var count = 0;
        var present = new string[Known.Length];
        foreach (var name in Known)
        {
            if (Directory.Exists(Path.Combine(repoRoot, name)))
                present[count++] = name;
        }
        if (count == Known.Length) return present;
        var trimmed = new string[count];
        Array.Copy(present, trimmed, count);
        return trimmed;
    }
}
