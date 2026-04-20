namespace Dotai.Services;

public static class RepoRootResolver
{
    public static string? Find(string startDir)
    {
        var dir = Path.GetFullPath(startDir);
        while (!string.IsNullOrEmpty(dir))
        {
            var dotGit = Path.Combine(dir, ".git");
            if (Directory.Exists(dotGit) || File.Exists(dotGit)) return dir;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) return null;
            dir = parent;
        }
        return null;
    }
}
