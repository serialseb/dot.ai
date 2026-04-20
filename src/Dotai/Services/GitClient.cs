using System.Text;
using Dotai.Text;

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
    public static byte[] DeriveCloneName(FastString url)
    {
        var bytes = url.Bytes;
        // Trim trailing slashes
        while (!bytes.IsEmpty && bytes[^1] == (byte)'/') bytes = bytes[..^1];
        // Strip trailing .git (4 bytes)
        if (bytes.EndsWith(".git"u8)) bytes = bytes[..^4];
        // Find last two slash positions
        int last = bytes.LastIndexOf((byte)'/');
        if (last < 0)
        {
            // No slash: replace all slashes (none here) — return as-is with slashes as underscores
            var copy = bytes.ToArray();
            for (int i = 0; i < copy.Length; i++)
                if (copy[i] == (byte)'/') copy[i] = (byte)'_';
            return copy;
        }
        int secondLast = bytes[..last].LastIndexOf((byte)'/');
        ReadOnlySpan<byte> seg1 = secondLast >= 0 ? bytes[(secondLast + 1)..last] : bytes[..last];
        ReadOnlySpan<byte> seg2 = bytes[(last + 1)..];
        // Join with '_'
        var result = new byte[seg1.Length + 1 + seg2.Length];
        seg1.CopyTo(result);
        result[seg1.Length] = (byte)'_';
        seg2.CopyTo(result.AsSpan(seg1.Length + 1));
        return result;
    }
}
