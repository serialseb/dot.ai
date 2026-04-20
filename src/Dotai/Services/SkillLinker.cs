namespace Dotai.Services;

public static class SkillLinker
{
    public static void LinkSkills(
        string repoRoot, string clone, IReadOnlyList<string> agents, SyncReport report)
    {
        var skillsDir = Path.Combine(clone, "skills");
        if (!Directory.Exists(skillsDir)) return;

        foreach (var skillPath in Directory.EnumerateDirectories(skillsDir))
        {
            var skillName = Path.GetFileName(skillPath);
            foreach (var agent in agents)
            {
                var targetDir = Path.Combine(repoRoot, agent, "skills");
                Directory.CreateDirectory(targetDir);
                var target = Path.Combine(targetDir, skillName);
                if (EnsureSymlink(target, skillPath, report)) report.SkillsLinked++;
            }
        }
    }

    public static void LinkFiles(string repoRoot, string clone, SyncReport report)
    {
        var filesDir = Path.Combine(clone, "files");
        if (!Directory.Exists(filesDir)) return;

        foreach (var filePath in Directory.EnumerateFiles(filesDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(filesDir, filePath);
            var target = Path.Combine(repoRoot, rel);
            var parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            if (EnsureSymlink(target, filePath, report)) report.FilesLinked++;
        }
    }

    public static void CleanupOrphans(string repoRoot, IReadOnlyList<string> agents)
    {
        var ownedPrefix = Path.GetFullPath(Path.Combine(repoRoot, ".ai", "repositories"))
            + Path.DirectorySeparatorChar;

        foreach (var agent in agents)
        {
            var dir = Path.Combine(repoRoot, agent, "skills");
            if (!Directory.Exists(dir)) continue;
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
            {
                RemoveIfDanglingAndOwned(entry, ownedPrefix);
            }
        }

    }

    public static void ForceReset(string repoRoot, IReadOnlyList<string> agents)
    {
        var ownedPrefix = Path.GetFullPath(Path.Combine(repoRoot, ".ai", "repositories"))
            + Path.DirectorySeparatorChar;

        foreach (var agent in agents)
        {
            var dir = Path.Combine(repoRoot, agent, "skills");
            if (!Directory.Exists(dir)) continue;
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
                RemoveIfOwned(entry, ownedPrefix);
        }

        RemoveOwnedFileSymlinksInTree(repoRoot, ownedPrefix);
    }

    private static void RemoveOwnedFileSymlinksInTree(string repoRoot, string ownedPrefix)
    {
        foreach (var entry in Directory.EnumerateFiles(repoRoot, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(repoRoot, entry);
            if (rel.StartsWith(".ai" + Path.DirectorySeparatorChar) || rel == ".ai") continue;
            if (rel.StartsWith(".git" + Path.DirectorySeparatorChar) || rel == ".git") continue;
            RemoveIfOwned(entry, ownedPrefix);
        }
    }

    private static void RemoveIfOwned(string path, string ownedPrefix)
    {
        var info = new FileInfo(path);
        var target = info.LinkTarget;
        if (target == null) return;
        var absolute = Path.IsPathRooted(target)
            ? target
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path) ?? ".", target));
        if (!absolute.StartsWith(ownedPrefix)) return;
        File.Delete(path);
    }

    private static void RemoveIfDanglingAndOwned(string path, string ownedPrefix)
    {
        var info = new FileInfo(path);
        var target = info.LinkTarget;
        if (target == null) return;
        var absolute = Path.IsPathRooted(target)
            ? target
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path) ?? ".", target));
        if (!absolute.StartsWith(ownedPrefix)) return;
        if (File.Exists(absolute) || Directory.Exists(absolute)) return;
        File.Delete(path);
    }

    private static bool EnsureSymlink(string target, string source, SyncReport report)
    {
        var info = new FileInfo(target);
        if (info.Exists || Directory.Exists(target))
        {
            var existingLink = info.LinkTarget;
            if (existingLink == null)
            {
                report.Conflicts.Add($"{target}: exists as real file/directory, not a symlink");
                return false;
            }
            if (string.Equals(Path.GetFullPath(existingLink), Path.GetFullPath(source),
                StringComparison.Ordinal)) return false;

            var existingAbsolute = Path.IsPathRooted(existingLink)
                ? existingLink
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(target) ?? ".", existingLink));

            if (IsInDifferentClone(source, existingAbsolute))
            {
                report.Conflicts.Add(
                    $"{Path.GetFileName(target)} provided by {source} and {existingAbsolute}");
                return false;
            }

            File.Delete(target);
        }

        if (Directory.Exists(source)) Directory.CreateSymbolicLink(target, source);
        else File.CreateSymbolicLink(target, source);
        return true;
    }

    private static bool IsInDifferentClone(string a, string b)
    {
        var fa = Path.GetFullPath(a);
        var fb = Path.GetFullPath(b);
        var marker = Path.DirectorySeparatorChar + ".ai" + Path.DirectorySeparatorChar
            + "repositories" + Path.DirectorySeparatorChar;
        var ia = fa.IndexOf(marker, StringComparison.Ordinal);
        var ib = fb.IndexOf(marker, StringComparison.Ordinal);
        if (ia < 0 || ib < 0) return false;
        var cloneA = fa[..(ia + marker.Length)] + fa[(ia + marker.Length)..].Split(Path.DirectorySeparatorChar)[0];
        var cloneB = fb[..(ib + marker.Length)] + fb[(ib + marker.Length)..].Split(Path.DirectorySeparatorChar)[0];
        return !string.Equals(cloneA, cloneB, StringComparison.Ordinal);
    }
}
