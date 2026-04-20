using Dotai.Native;

namespace Dotai.Services;

public static class RepoRootResolver
{
    public static bool TryFind(NativeStringView startDir, out NativeString repoRoot)
    {
        if (!Fs.IsDirectory(startDir)) { repoRoot = default; return false; }
        var dir = Fs.GetFullPath(startDir);
        while (!dir.IsEmpty)
        {
            var dotGit = Fs.Combine(dir.AsView(), ".git"u8);
            bool exists = Fs.Exists(dotGit.AsView());
            dotGit.Dispose();
            if (exists) { repoRoot = dir; return true; }
            var parent = Fs.GetDirectoryName(dir.AsView());
            if (parent.IsEmpty || IsSame(parent.AsView(), dir.AsView()))
            {
                parent.Dispose();
                break;
            }
            dir.Dispose();
            dir = parent;
        }
        dir.Dispose();
        repoRoot = default;
        return false;
    }

    private static bool IsSame(NativeStringView a, NativeStringView b)
        => a.Bytes.SequenceEqual(b.Bytes);
}
