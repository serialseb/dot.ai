using System.Text;
using Dotai.Text;

namespace Dotai.Services;

public static class RepoRootResolver
{
    // Byte-native API used by production code.
    public static bool TryFind(FastString startDir, out byte[] repoRoot)
    {
        if (!Fs.IsDirectory(startDir)) { repoRoot = []; return false; }
        var dir = Fs.GetFullPath(startDir);
        while (dir.Length > 0)
        {
            var dotGit = Fs.Combine(dir, ".git"u8);
            if (Fs.Exists(dotGit)) { repoRoot = dir; return true; }
            var parent = Fs.GetDirectoryName(dir);
            if (parent.Length == 0 || IsSameBytes(parent, dir)) break;
            dir = parent;
        }
        repoRoot = [];
        return false;
    }

    // String shim retained for tests (tests stay in .NET-land).
    public static string? Find(string startDir)
    {
        FastString fs = Encoding.UTF8.GetBytes(startDir);
        if (!TryFind(fs, out var root)) return null;
        return Encoding.UTF8.GetString(root);
    }

    private static bool IsSameBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}
