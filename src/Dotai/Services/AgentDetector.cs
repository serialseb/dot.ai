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

    // String shim retained for tests (tests stay in .NET-land).
    public static string[] Detect(string repoRoot)
    {
        var roots = System.Text.Encoding.UTF8.GetBytes(repoRoot);
        var byteResult = Detect((FastString)roots);
        var result = new string[byteResult.Length];
        for (int i = 0; i < byteResult.Length; i++)
            result[i] = System.Text.Encoding.UTF8.GetString(byteResult[i]);
        return result;
    }
}
