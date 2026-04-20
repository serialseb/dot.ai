using Dotai.Native;

namespace Dotai.Services;

public static class AgentDetector
{
    public static NativeList<NativeString> Detect(NativeStringView repoRoot)
    {
        var result = new NativeList<NativeString>(3);
        Check(repoRoot, ".claude"u8, ref result);
        Check(repoRoot, ".codex"u8, ref result);
        Check(repoRoot, ".opencode"u8, ref result);
        return result;
    }

    private static void Check(NativeStringView repoRoot, ReadOnlySpan<byte> name, ref NativeList<NativeString> result)
    {
        NativeStringView nameView = name;
        var combined = Fs.Combine(repoRoot, nameView);
        if (Fs.IsDirectory(combined.AsView()))
            result.Add(NativeString.From(nameView));
        combined.Dispose();
    }
}
