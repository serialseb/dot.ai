using Dotai.Text;

namespace Dotai.Services;

public static class AgentDetector
{
    private static readonly byte[][] Known =
    [
        ".claude"u8.ToArray(),
        ".codex"u8.ToArray(),
        ".opencode"u8.ToArray(),
    ];

    // Byte-native API used by production code.
    public static byte[][] Detect(FastString repoRoot)
    {
        var count = 0;
        var present = new byte[Known.Length][];
        foreach (var name in Known)
        {
            if (Fs.IsDirectory(Fs.Combine(repoRoot, name)))
                present[count++] = name;
        }
        if (count == Known.Length) return present;
        var trimmed = new byte[count][];
        Array.Copy(present, trimmed, count);
        return trimmed;
    }


}
