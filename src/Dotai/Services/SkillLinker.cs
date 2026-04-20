using Dotai.Text;
using Dotai.Ui;

namespace Dotai.Services;

public static class SkillLinker
{
    public static void LinkSkills(
        FastString repoRoot, FastString clone, ReadOnlySpan<Arg> agents, SyncReport report)
    {
        var skillsDir = Fs.Combine(clone, "skills"u8);
        if (!Fs.IsDirectory(skillsDir)) return;

        var skillPaths = (System.Collections.Generic.List<byte[]>)Fs.EnumerateDirectories(skillsDir);
        for (int si = 0; si < skillPaths.Count; si++)
        {
            var skillPath = skillPaths[si];
            var skillName = Fs.GetFileName(skillPath);
            for (int ai = 0; ai < agents.Length; ai++)
            {
                var targetDir = Fs.Combine(Fs.Combine(repoRoot, agents[ai].AsFast), "skills"u8);
                Fs.TryCreateDirectory(targetDir);
                var target = Fs.Combine(targetDir, skillName);
                if (EnsureSymlink(target, skillPath, report)) report.SkillsLinked++;
            }
        }
    }

    public static void LinkFiles(FastString repoRoot, FastString clone, SyncReport report)
    {
        var filesDir = Fs.Combine(clone, "files"u8);
        if (!Fs.IsDirectory(filesDir)) return;

        var filePaths = (System.Collections.Generic.List<byte[]>)Fs.EnumerateFiles(filesDir, recursive: true);
        for (int i = 0; i < filePaths.Count; i++)
        {
            var filePath = filePaths[i];
            var rel = Fs.GetRelativePath(filesDir, filePath);
            var target = Fs.Combine(repoRoot, rel);
            var parent = Fs.GetDirectoryName(target);
            if (parent.Length > 0) Fs.TryCreateDirectory(parent);
            if (EnsureSymlink(target, filePath, report)) report.FilesLinked++;
        }
    }

    public static void CleanupOrphans(FastString repoRoot, ReadOnlySpan<Arg> agents)
    {
        var ownedPrefix = Fs.Combine(Fs.Combine(repoRoot, ".ai"u8), "repositories"u8);

        for (int ai = 0; ai < agents.Length; ai++)
        {
            var dir = Fs.Combine(Fs.Combine(repoRoot, agents[ai].AsFast), "skills"u8);
            if (!Fs.IsDirectory(dir)) continue;
            var entries = (System.Collections.Generic.List<byte[]>)Fs.EnumerateFileSystemEntries(dir);
            for (int i = 0; i < entries.Count; i++)
                RemoveIfDanglingAndOwned(entries[i], ownedPrefix);
        }
    }

    public static void ForceReset(FastString repoRoot, ReadOnlySpan<Arg> agents)
    {
        var ownedPrefix = Fs.Combine(Fs.Combine(repoRoot, ".ai"u8), "repositories"u8);

        for (int ai = 0; ai < agents.Length; ai++)
        {
            var dir = Fs.Combine(Fs.Combine(repoRoot, agents[ai].AsFast), "skills"u8);
            if (!Fs.IsDirectory(dir)) continue;
            var entries = (System.Collections.Generic.List<byte[]>)Fs.EnumerateFileSystemEntries(dir);
            for (int i = 0; i < entries.Count; i++)
                RemoveIfOwned(entries[i], ownedPrefix);
        }

        RemoveOwnedFileSymlinksInTree(repoRoot.Bytes.ToArray(), ownedPrefix);
    }

    private static void RemoveOwnedFileSymlinksInTree(byte[] repoRoot, byte[] ownedPrefix)
    {
        var aiDir = Fs.Combine(repoRoot, ".ai"u8);
        var gitDir = Fs.Combine(repoRoot, ".git"u8);
        RemoveOwnedSymlinksRecursive(repoRoot, aiDir, gitDir, ownedPrefix);
    }

    private static void RemoveOwnedSymlinksRecursive(byte[] dir, byte[] aiDir, byte[] gitDir, byte[] ownedPrefix)
    {
        var entries = (System.Collections.Generic.List<byte[]>)Fs.EnumerateFileSystemEntries(dir);
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
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
        Fs.TryDeleteFile(path);
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
        Fs.TryDeleteFile(path);
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
                var buf = new ByteBuffer(target.Length + 48);
                buf.Append(target);
                buf.Append(": exists as real file/directory, not a symlink"u8);
                report.Conflicts.Add(buf.Span.ToArray());
                return false;
            }

            var existingAbsolute = existingLinkTarget.Length > 0 && existingLinkTarget[0] == (byte)'/'
                ? existingLinkTarget
                : Fs.Combine(Fs.GetDirectoryName(target), existingLinkTarget);

            if (SamePath(existingAbsolute, Fs.GetFullPath(source))) return false;

            if (IsInDifferentClone(source, existingAbsolute))
            {
                var name = Fs.GetFileName(target);
                var buf = new ByteBuffer(name.Length + source.Length + existingAbsolute.Length + 20);
                buf.Append(name);
                buf.Append(" provided by "u8);
                buf.Append(source);
                buf.Append(" and "u8);
                buf.Append(existingAbsolute);
                report.Conflicts.Add(buf.Span.ToArray());
                return false;
            }

            Fs.TryDeleteFile(target);
        }

        Fs.TryCreateSymbolicLink(target, source);
        return true;
    }

    private static bool IsInDifferentClone(byte[] a, byte[] b)
    {
        var fa = Fs.GetFullPath(a);
        var fb = Fs.GetFullPath(b);
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
