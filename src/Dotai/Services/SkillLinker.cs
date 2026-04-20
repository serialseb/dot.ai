using System.Text;
using Dotai.Text;

namespace Dotai.Services;

public static class SkillLinker
{
    public static void LinkSkills(
        string repoRoot, string clone, ReadOnlySpan<string> agents, SyncReport report)
    {
        var cloneBytes = Encoding.UTF8.GetBytes(clone);
        var repoRootBytes = Encoding.UTF8.GetBytes(repoRoot);
        var skillsDir = Fs.Combine(cloneBytes, "skills"u8);
        if (!Fs.IsDirectory(skillsDir)) return;

        foreach (var skillPath in Fs.EnumerateDirectories(skillsDir))
        {
            var skillName = Fs.GetFileName(skillPath);
            foreach (var agent in agents)
            {
                var agentBytes = Encoding.UTF8.GetBytes(agent);
                var targetDir = Fs.Combine(Fs.Combine(repoRootBytes, agentBytes), "skills"u8);
                Fs.CreateDirectory(targetDir);
                var target = Fs.Combine(targetDir, skillName);
                if (EnsureSymlink(target, skillPath, report)) report.SkillsLinked++;
            }
        }
    }

    public static void LinkFiles(string repoRoot, string clone, SyncReport report)
    {
        var cloneBytes = Encoding.UTF8.GetBytes(clone);
        var repoRootBytes = Encoding.UTF8.GetBytes(repoRoot);
        var filesDir = Fs.Combine(cloneBytes, "files"u8);
        if (!Fs.IsDirectory(filesDir)) return;

        foreach (var filePath in Fs.EnumerateFiles(filesDir, recursive: true))
        {
            var rel = Fs.GetRelativePath(filesDir, filePath);
            var target = Fs.Combine(repoRootBytes, rel);
            var parent = Fs.GetDirectoryName(target);
            if (parent.Length > 0) Fs.CreateDirectory(parent);
            if (EnsureSymlink(target, filePath, report)) report.FilesLinked++;
        }
    }

    public static void CleanupOrphans(string repoRoot, ReadOnlySpan<string> agents)
    {
        var repoRootBytes = Encoding.UTF8.GetBytes(repoRoot);
        var ownedPrefix = Fs.Combine(Fs.Combine(repoRootBytes, ".ai"u8), "repositories"u8);

        foreach (var agent in agents)
        {
            var agentBytes = Encoding.UTF8.GetBytes(agent);
            var dir = Fs.Combine(Fs.Combine(repoRootBytes, agentBytes), "skills"u8);
            if (!Fs.IsDirectory(dir)) continue;
            foreach (var entry in Fs.EnumerateFileSystemEntries(dir))
                RemoveIfDanglingAndOwned(entry, ownedPrefix);
        }
    }

    public static void ForceReset(string repoRoot, ReadOnlySpan<string> agents)
    {
        var repoRootBytes = Encoding.UTF8.GetBytes(repoRoot);
        var ownedPrefix = Fs.Combine(Fs.Combine(repoRootBytes, ".ai"u8), "repositories"u8);

        foreach (var agent in agents)
        {
            var agentBytes = Encoding.UTF8.GetBytes(agent);
            var dir = Fs.Combine(Fs.Combine(repoRootBytes, agentBytes), "skills"u8);
            if (!Fs.IsDirectory(dir)) continue;
            foreach (var entry in Fs.EnumerateFileSystemEntries(dir))
                RemoveIfOwned(entry, ownedPrefix);
        }

        RemoveOwnedFileSymlinksInTree(repoRootBytes, ownedPrefix);
    }

    private static void RemoveOwnedFileSymlinksInTree(byte[] repoRoot, byte[] ownedPrefix)
    {
        var aiDir = Fs.Combine(repoRoot, ".ai"u8);
        var gitDir = Fs.Combine(repoRoot, ".git"u8);
        RemoveOwnedSymlinksRecursive(repoRoot, aiDir, gitDir, ownedPrefix);
    }

    private static void RemoveOwnedSymlinksRecursive(byte[] dir, byte[] aiDir, byte[] gitDir, byte[] ownedPrefix)
    {
        foreach (var entry in Fs.EnumerateFileSystemEntries(dir))
        {
            if (StartsWith(entry, aiDir) || StartsWith(entry, gitDir)) continue;
            if (Fs.IsSymlink(entry))
            {
                RemoveIfOwned(entry, ownedPrefix);
            }
            else if (Fs.IsDirectory(entry))
            {
                RemoveOwnedSymlinksRecursive(entry, aiDir, gitDir, ownedPrefix);
            }
        }
    }

    private static void RemoveIfOwned(byte[] path, byte[] ownedPrefix)
    {
        var target = Fs.ReadSymbolicLinkTarget(path);
        if (target == null) return;
        var absolute = target.Length > 0 && target[0] == (byte)'/'
            ? target
            : Fs.Combine(Fs.GetDirectoryName(path), target);
        if (!StartsWith(absolute, ownedPrefix)) return;
        Fs.DeleteFile(path);
    }

    private static void RemoveIfDanglingAndOwned(byte[] path, byte[] ownedPrefix)
    {
        var target = Fs.ReadSymbolicLinkTarget(path);
        if (target == null) return;
        var absolute = target.Length > 0 && target[0] == (byte)'/'
            ? target
            : Fs.Combine(Fs.GetDirectoryName(path), target);
        if (!StartsWith(absolute, ownedPrefix)) return;
        if (Fs.Exists(absolute)) return;
        Fs.DeleteFile(path);
    }

    private static bool EnsureSymlink(byte[] target, byte[] source, SyncReport report)
    {
        bool targetExists = Fs.Exists(target);
        bool targetIsLink = Fs.IsSymlink(target);

        if (targetExists || targetIsLink)
        {
            var existingLinkTarget = Fs.ReadSymbolicLinkTarget(target);
            if (existingLinkTarget == null)
            {
                report.Conflicts.Add(
                    $"{Encoding.UTF8.GetString(target)}: exists as real file/directory, not a symlink");
                return false;
            }

            var existingAbsolute = existingLinkTarget.Length > 0 && existingLinkTarget[0] == (byte)'/'
                ? existingLinkTarget
                : Fs.Combine(Fs.GetDirectoryName(target), existingLinkTarget);

            // Already points at the right place?
            if (SamePath(existingAbsolute, Fs.GetFullPath(source))) return false;

            if (IsInDifferentClone(source, existingAbsolute))
            {
                report.Conflicts.Add(
                    $"{Encoding.UTF8.GetString(Fs.GetFileName(target))} provided by " +
                    $"{Encoding.UTF8.GetString(source)} and {Encoding.UTF8.GetString(existingAbsolute)}");
                return false;
            }

            Fs.DeleteFile(target);
        }

        Fs.CreateSymbolicLink(target, source);
        return true;
    }

    private static bool IsInDifferentClone(byte[] a, byte[] b)
    {
        var fa = Fs.GetFullPath(a);
        var fb = Fs.GetFullPath(b);
        // marker: /.ai/repositories/
        var marker = "/.ai/repositories/"u8;
        int ia = IndexOf(fa, marker);
        int ib = IndexOf(fb, marker);
        if (ia < 0 || ib < 0) return false;
        var cloneA = CloneRootOf(fa, ia + marker.Length);
        var cloneB = CloneRootOf(fb, ib + marker.Length);
        return !SamePath(cloneA, cloneB);
    }

    private static byte[] CloneRootOf(byte[] path, int afterMarker)
    {
        int slash = IndexOf(path.AsSpan(afterMarker), (byte)'/');
        return slash < 0 ? path : path[..(afterMarker + slash)];
    }

    private static int IndexOf(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
            if (haystack.Slice(i, needle.Length).SequenceEqual(needle)) return i;
        return -1;
    }

    private static int IndexOf(ReadOnlySpan<byte> haystack, byte b)
        => haystack.IndexOf(b);

    private static bool StartsWith(byte[] path, byte[] prefix)
    {
        if (path.Length < prefix.Length) return false;
        return path.AsSpan(0, prefix.Length).SequenceEqual(prefix);
    }

    private static bool SamePath(byte[] a, byte[] b)
        => a.AsSpan().SequenceEqual(b);
}
