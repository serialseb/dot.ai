using System.Text;

namespace Dotai.Services;

public sealed class GitResult
{
    public int ExitCode { get; }
    public byte[] StdOut { get; }
    public byte[] StdErr { get; }
    public GitResult(int exit, byte[] stdout, byte[] stderr) { ExitCode = exit; StdOut = stdout; StdErr = stderr; }
}

public static class GitClient
{
    // Passes "-C <workDir>" as the first arguments so posix_spawnp does not
    // need a working-directory parameter (posix_spawn has no direct equivalent
    // of ProcessStartInfo.WorkingDirectory).
    public static GitResult Run(string workDir, params string[] args)
    {
        var argv = BuildArgv(workDir, args);
        var (exitCode, stdout, stderr) = PosixSpawn.Run("git", argv);
        return new GitResult(exitCode, stdout, stderr);
    }

    private static string[] BuildArgv(string workDir, string[] args)
    {
        // argv[0] is the executable name, then -C <workDir>, then the caller's args.
        var argv = new string[1 + 2 + args.Length];
        argv[0] = "git";
        argv[1] = "-C";
        argv[2] = workDir;
        args.CopyTo(argv, 3);
        return argv;
    }

    public static GitResult Clone(string url, string target)
    {
        var parent = Path.GetDirectoryName(target) ?? ".";
        Directory.CreateDirectory(parent);
        return Run(parent, "clone", url, target);
    }

    public static GitResult StatusPorcelain(string workDir) =>
        Run(workDir, "status", "--porcelain");

    public static GitResult AddAll(string workDir) =>
        Run(workDir, "add", "-A");

    public static GitResult Commit(string workDir, string message) =>
        Run(workDir, "commit", "-m", message);

    public static GitResult Fetch(string workDir) =>
        Run(workDir, "fetch", "origin");

    public static GitResult Rebase(string workDir, string upstream) =>
        Run(workDir, "rebase", upstream);

    public static GitResult Push(string workDir, string branch) =>
        Run(workDir, "push", "origin", branch);

    public static byte[] DefaultBranch(string workDir)
    {
        var r = Run(workDir, "symbolic-ref", "refs/remotes/origin/HEAD");
        if (r.ExitCode != 0) return "main"u8.ToArray();
        return ByteOps.GetDefaultBranchFromSymbolicRef(r.StdOut).ToArray();
    }

    public static bool RebaseInProgress(string workDir) =>
        Directory.Exists(Path.Combine(workDir, ".git", "rebase-merge"))
        || Directory.Exists(Path.Combine(workDir, ".git", "rebase-apply"));

    /// <summary>
    /// Derives a filesystem-safe clone directory name from a remote URL.
    /// Both InitCommand and SyncCommand must use this method so the clone
    /// directory name is always consistent regardless of how it was registered.
    /// </summary>
    public static string DeriveCloneName(string url)
    {
        var trimmed = url.TrimEnd('/');
        if (trimmed.EndsWith(".git")) trimmed = trimmed[..^4];
        var segs = trimmed.Split('/');
        if (segs.Length < 2) return trimmed.Replace('/', '_');
        return $"{segs[^2]}_{segs[^1]}";
    }
}
