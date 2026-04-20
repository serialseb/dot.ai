using Dotai.Native;
using Dotai.Ui;

namespace Dotai.Services;

public static unsafe class SkillLinker
{
    public static void LinkSkills(
        NativeStringView repoRoot, NativeStringView clone, NativeListView<NativeString> agents, ref SyncReport report)
    {
        var skillsDir = Fs.Combine(clone, "skills"u8);
        if (!Fs.IsDirectory(skillsDir.AsView()))
        {
            skillsDir.Dispose();
            return;
        }

        var skillPaths = Fs.EnumerateDirectories(skillsDir.AsView());
        skillsDir.Dispose();

        for (int si = 0; si < skillPaths.Length; si++)
        {
            var skillPath = skillPaths[si].AsView();
            var skillName = Fs.GetFileName(skillPath);
            for (int ai = 0; ai < agents.Length; ai++)
            {
                var agentDir = Fs.Combine(repoRoot, agents[ai].AsView());
                var targetDir = Fs.Combine(agentDir.AsView(), "skills"u8);
                agentDir.Dispose();
                Fs.TryCreateDirectory(targetDir.AsView());
                var target = Fs.Combine(targetDir.AsView(), skillName.AsView());
                targetDir.Dispose();
                if (EnsureSymlink(target.AsView(), skillPath, ref report)) report.SkillsLinked++;
                target.Dispose();
            }
            skillName.Dispose();
        }
        for (int i = 0; i < skillPaths.Length; i++) skillPaths[i].Dispose();
        skillPaths.Dispose();
    }

    public static void LinkFiles(NativeStringView repoRoot, NativeStringView clone, ref SyncReport report)
    {
        var filesDir = Fs.Combine(clone, "files"u8);
        if (!Fs.IsDirectory(filesDir.AsView()))
        {
            filesDir.Dispose();
            return;
        }

        var filePaths = Fs.EnumerateFiles(filesDir.AsView(), recursive: true);
        for (int i = 0; i < filePaths.Length; i++)
        {
            var filePath = filePaths[i].AsView();
            var rel = Fs.GetRelativePath(filesDir.AsView(), filePath);
            var target = Fs.Combine(repoRoot, rel.AsView());
            rel.Dispose();
            var parent = Fs.GetDirectoryName(target.AsView());
            if (!parent.IsEmpty) Fs.TryCreateDirectory(parent.AsView());
            parent.Dispose();
            if (EnsureSymlink(target.AsView(), filePath, ref report)) report.FilesLinked++;
            target.Dispose();
        }
        filesDir.Dispose();
        for (int i = 0; i < filePaths.Length; i++) filePaths[i].Dispose();
        filePaths.Dispose();
    }

    public static void CleanupOrphans(NativeStringView repoRoot, NativeListView<NativeString> agents)
    {
        var aiDir = Fs.Combine(repoRoot, ".ai"u8);
        var ownedPrefix = Fs.Combine(aiDir.AsView(), "repositories"u8);
        aiDir.Dispose();

        for (int ai = 0; ai < agents.Length; ai++)
        {
            var agentDir = Fs.Combine(repoRoot, agents[ai].AsView());
            var dir = Fs.Combine(agentDir.AsView(), "skills"u8);
            agentDir.Dispose();
            if (!Fs.IsDirectory(dir.AsView())) { dir.Dispose(); continue; }
            var entries = Fs.EnumerateFileSystemEntries(dir.AsView());
            dir.Dispose();
            for (int i = 0; i < entries.Length; i++)
                RemoveIfDanglingAndOwned(entries[i].AsView(), ownedPrefix.AsView());
            for (int i = 0; i < entries.Length; i++) entries[i].Dispose();
            entries.Dispose();
        }
        ownedPrefix.Dispose();
    }

    public static void ForceReset(NativeStringView repoRoot, NativeListView<NativeString> agents)
    {
        var aiDir = Fs.Combine(repoRoot, ".ai"u8);
        var ownedPrefix = Fs.Combine(aiDir.AsView(), "repositories"u8);
        aiDir.Dispose();

        for (int ai = 0; ai < agents.Length; ai++)
        {
            var agentDir = Fs.Combine(repoRoot, agents[ai].AsView());
            var dir = Fs.Combine(agentDir.AsView(), "skills"u8);
            agentDir.Dispose();
            if (!Fs.IsDirectory(dir.AsView())) { dir.Dispose(); continue; }
            var entries = Fs.EnumerateFileSystemEntries(dir.AsView());
            dir.Dispose();
            for (int i = 0; i < entries.Length; i++)
                RemoveIfOwned(entries[i].AsView(), ownedPrefix.AsView());
            for (int i = 0; i < entries.Length; i++) entries[i].Dispose();
            entries.Dispose();
        }

        RemoveOwnedFileSymlinksInTree(repoRoot, ownedPrefix.AsView());
        ownedPrefix.Dispose();
    }

    private static void RemoveOwnedFileSymlinksInTree(NativeStringView repoRoot, NativeStringView ownedPrefix)
    {
        var aiDir = Fs.Combine(repoRoot, ".ai"u8);
        var gitDir = Fs.Combine(repoRoot, ".git"u8);
        RemoveOwnedSymlinksRecursive(repoRoot, aiDir.AsView(), gitDir.AsView(), ownedPrefix);
        aiDir.Dispose();
        gitDir.Dispose();
    }

    private static void RemoveOwnedSymlinksRecursive(
        NativeStringView dir, NativeStringView aiDir, NativeStringView gitDir, NativeStringView ownedPrefix)
    {
        var entries = Fs.EnumerateFileSystemEntries(dir);
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i].AsView();
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
        for (int i = 0; i < entries.Length; i++) entries[i].Dispose();
        entries.Dispose();
    }

    private static void RemoveIfOwned(NativeStringView path, NativeStringView ownedPrefix)
    {
        if (!Fs.TryReadSymbolicLinkTarget(path, out var target)) return;
        NativeString absolute;
        if (target.Length > 0 && target.AsView().Bytes[0] == (byte)'/')
        {
            absolute = NativeString.From(target.AsView());
        }
        else
        {
            var dirName = Fs.GetDirectoryName(path);
            absolute = Fs.Combine(dirName.AsView(), target.AsView());
            dirName.Dispose();
        }
        target.Dispose();
        if (!StartsWith(absolute.AsView(), ownedPrefix)) { absolute.Dispose(); return; }
        absolute.Dispose();
        Fs.TryDeleteFile(path);
    }

    private static void RemoveIfDanglingAndOwned(NativeStringView path, NativeStringView ownedPrefix)
    {
        if (!Fs.TryReadSymbolicLinkTarget(path, out var target)) return;
        NativeString absolute;
        if (target.Length > 0 && target.AsView().Bytes[0] == (byte)'/')
        {
            absolute = NativeString.From(target.AsView());
        }
        else
        {
            var dirName = Fs.GetDirectoryName(path);
            absolute = Fs.Combine(dirName.AsView(), target.AsView());
            dirName.Dispose();
        }
        target.Dispose();
        if (!StartsWith(absolute.AsView(), ownedPrefix)) { absolute.Dispose(); return; }
        if (Fs.Exists(absolute.AsView())) { absolute.Dispose(); return; }
        absolute.Dispose();
        Fs.TryDeleteFile(path);
    }

    private static bool EnsureSymlink(NativeStringView target, NativeStringView source, ref SyncReport report)
    {
        bool targetExists = Fs.Exists(target);
        bool targetIsLink = Fs.IsSymlink(target);

        if (targetExists || targetIsLink)
        {
            if (!Fs.TryReadSymbolicLinkTarget(target, out var existingLinkTarget))
            {
                var buf = new NativeBuffer(target.Length + 48);
                buf.Append(target);
                buf.Append(": exists as real file/directory, not a symlink"u8);
                report.Conflicts.Add(buf.Freeze());
                return false;
            }

            NativeString existingAbsolute;
            if (existingLinkTarget.Length > 0 && existingLinkTarget.AsView().Bytes[0] == (byte)'/')
            {
                existingAbsolute = NativeString.From(existingLinkTarget.AsView());
            }
            else
            {
                var dirName = Fs.GetDirectoryName(target);
                existingAbsolute = Fs.Combine(dirName.AsView(), existingLinkTarget.AsView());
                dirName.Dispose();
            }
            existingLinkTarget.Dispose();

            var sourceFullPath = Fs.GetFullPath(source);
            bool samePath = existingAbsolute.AsView().Bytes.SequenceEqual(sourceFullPath.AsView().Bytes);
            sourceFullPath.Dispose();
            if (samePath) { existingAbsolute.Dispose(); return false; }

            if (IsInDifferentClone(source, existingAbsolute.AsView()))
            {
                var name = Fs.GetFileName(target);
                var buf = new NativeBuffer(name.Length + source.Length + existingAbsolute.Length + 20);
                buf.Append(name.AsView());
                buf.Append(" provided by "u8);
                buf.Append(source);
                buf.Append(" and "u8);
                buf.Append(existingAbsolute.AsView());
                name.Dispose();
                existingAbsolute.Dispose();
                report.Conflicts.Add(buf.Freeze());
                return false;
            }

            existingAbsolute.Dispose();
            Fs.TryDeleteFile(target);
        }

        Fs.TryCreateSymbolicLink(target, source);
        return true;
    }

    private static bool IsInDifferentClone(NativeStringView a, NativeStringView b)
    {
        var fa = Fs.GetFullPath(a);
        var fb = Fs.GetFullPath(b);
        NativeStringView marker = "/.ai/repositories/"u8;
        int ia = fa.AsView().IndexOf(marker);
        int ib = fb.AsView().IndexOf(marker);
        if (ia < 0 || ib < 0) { fa.Dispose(); fb.Dispose(); return false; }
        var cloneA = CloneRootOf(fa.AsView(), ia + marker.Length);
        var cloneB = CloneRootOf(fb.AsView(), ib + marker.Length);
        fa.Dispose(); fb.Dispose();
        bool diff = !cloneA.Bytes.SequenceEqual(cloneB.Bytes);
        return diff;
    }

    private static NativeStringView CloneRootOf(NativeStringView path, int afterMarker)
    {
        var tail = path.Bytes[afterMarker..];
        int slash = tail.IndexOf((byte)'/');
        return slash < 0 ? path : new NativeStringView(path.Bytes[..(afterMarker + slash)]);
    }

    private static bool StartsWith(NativeStringView path, NativeStringView prefix)
        => path.Bytes.StartsWith(prefix.Bytes);
}
