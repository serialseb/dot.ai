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
    // Null-terminated "git\0" for posix_spawnp — no string encoding needed.
    private static readonly byte[] GitExec = "git\0"u8.ToArray();

    // Passes "-C <workDir>" as the first arguments so posix_spawnp does not
    // need a working-directory parameter (posix_spawn has no direct equivalent
    // of ProcessStartInfo.WorkingDirectory).
    public static GitResult Run(FastString workDir, params byte[][] args)
    {
        var argv = BuildArgv(workDir, args);
        var (exitCode, stdout, stderr) = PosixSpawn.Run(GitExec, argv);
        return new GitResult(exitCode, stdout, stderr);
    }

    private static byte[][] BuildArgv(FastString workDir, byte[][] args)
    {
        // argv[0] = "git", argv[1] = "-C", argv[2] = workDir, argv[3..] = args
        var argv = new byte[1 + 2 + args.Length][];
        argv[0] = "git"u8.ToArray();
        argv[1] = "-C"u8.ToArray();
        argv[2] = workDir.Bytes.ToArray();
        for (int i = 0; i < args.Length; i++)
            argv[3 + i] = args[i];
        return argv;
    }

    public static GitResult Clone(FastString url, FastString target)
    {
        var parent = Fs.GetDirectoryName(target);
        if (parent.Length > 0) Fs.TryCreateDirectory(parent);
        // Clone uses the CWD (parent dir) as workDir, since target is absolute.
        var argv = new byte[][]
        {
            "git"u8.ToArray(),
            "clone"u8.ToArray(),
            url.Bytes.ToArray(),
            target.Bytes.ToArray(),
        };
        var (exitCode, stdout, stderr) = PosixSpawn.Run(GitExec, argv);
        return new GitResult(exitCode, stdout, stderr);
    }

    public static GitResult StatusPorcelain(FastString workDir) =>
        Run(workDir, "status"u8.ToArray(), "--porcelain"u8.ToArray());

    public static GitResult AddAll(FastString workDir) =>
        Run(workDir, "add"u8.ToArray(), "-A"u8.ToArray());

    public static GitResult Commit(FastString workDir, FastString message) =>
        Run(workDir, "commit"u8.ToArray(), "-m"u8.ToArray(), message.Bytes.ToArray());

    public static GitResult Fetch(FastString workDir) =>
        Run(workDir, "fetch"u8.ToArray(), "origin"u8.ToArray());

    public static GitResult Rebase(FastString workDir, FastString upstream) =>
        Run(workDir, "rebase"u8.ToArray(), upstream.Bytes.ToArray());

    public static GitResult Push(FastString workDir, FastString branch) =>
        Run(workDir, "push"u8.ToArray(), "origin"u8.ToArray(), branch.Bytes.ToArray());

    public static byte[] DefaultBranch(FastString workDir)
    {
        var r = Run(workDir, "symbolic-ref"u8.ToArray(), "refs/remotes/origin/HEAD"u8.ToArray());
        if (r.ExitCode != 0) return "main"u8.ToArray();
        return ByteOps.GetDefaultBranchFromSymbolicRef(r.StdOut).ToArray();
    }

    public static bool RebaseInProgress(FastString workDir)
        => Fs.IsDirectory(Fs.Combine(Fs.Combine(workDir, ".git"u8), "rebase-merge"u8))
        || Fs.IsDirectory(Fs.Combine(Fs.Combine(workDir, ".git"u8), "rebase-apply"u8));

    /// <summary>
    /// Derives a filesystem-safe clone directory name from a remote URL.
    /// Both InitCommand and SyncCommand must use this method so the clone
    /// directory name is always consistent regardless of how it was registered.
    /// </summary>
    public static byte[] DeriveCloneName(FastString url)
    {
        var bytes = url.Bytes;
        while (!bytes.IsEmpty && bytes[^1] == (byte)'/') bytes = bytes[..^1];
        if (bytes.EndsWith(".git"u8)) bytes = bytes[..^4];
        int last = bytes.LastIndexOf((byte)'/');
        if (last < 0)
        {
            var copy = bytes.ToArray();
            for (int i = 0; i < copy.Length; i++)
                if (copy[i] == (byte)'/') copy[i] = (byte)'_';
            return copy;
        }
        int secondLast = bytes[..last].LastIndexOf((byte)'/');
        ReadOnlySpan<byte> seg1 = secondLast >= 0 ? bytes[(secondLast + 1)..last] : bytes[..last];
        ReadOnlySpan<byte> seg2 = bytes[(last + 1)..];
        var result = new byte[seg1.Length + 1 + seg2.Length];
        seg1.CopyTo(result);
        result[seg1.Length] = (byte)'_';
        seg2.CopyTo(result.AsSpan(seg1.Length + 1));
        return result;
    }
}
