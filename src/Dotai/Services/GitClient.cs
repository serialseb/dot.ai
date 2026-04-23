using Dotai.Native;

namespace Dotai.Services;

public struct GitResult
{
    public int ExitCode;
    public NativeString StdOut;
    public NativeString StdErr;
    public GitResult(int exit, NativeString stdout, NativeString stderr) { ExitCode = exit; StdOut = stdout; StdErr = stderr; }
    public void Dispose() { StdOut.Dispose(); StdErr.Dispose(); }
}

public static unsafe class GitClient
{
    public static GitResult Run(NativeStringView workDir, params NativeString[] args)
    {
        // Build: git -C <workDir> [args...]
        var argv = new NativeList<NativeString>(3 + args.Length);
        argv.Add(NativeString.From("git"u8));
        argv.Add(NativeString.From("-C"u8));
        argv.Add(NativeString.From(workDir));
        for (int i = 0; i < args.Length; i++)
            argv.Add(NativeString.From(args[i].AsView()));
        var result = PosixSpawn.Run("git"u8, argv.AsView());
        for (int i = 0; i < argv.Length; i++) argv[i].Dispose();
        argv.Dispose();
        return result;
    }

    // Overload accepting NativeStringView args (literal u8 strings)
    public static GitResult Run(NativeStringView workDir, NativeStringView arg0)
    {
        var argv = new NativeList<NativeString>(4);
        argv.Add(NativeString.From("git"u8));
        argv.Add(NativeString.From("-C"u8));
        argv.Add(NativeString.From(workDir));
        argv.Add(NativeString.From(arg0));
        var result = PosixSpawn.Run("git"u8, argv.AsView());
        for (int i = 0; i < argv.Length; i++) argv[i].Dispose();
        argv.Dispose();
        return result;
    }

    public static GitResult Run(NativeStringView workDir, NativeStringView arg0, NativeStringView arg1)
    {
        var argv = new NativeList<NativeString>(5);
        argv.Add(NativeString.From("git"u8));
        argv.Add(NativeString.From("-C"u8));
        argv.Add(NativeString.From(workDir));
        argv.Add(NativeString.From(arg0));
        argv.Add(NativeString.From(arg1));
        var result = PosixSpawn.Run("git"u8, argv.AsView());
        for (int i = 0; i < argv.Length; i++) argv[i].Dispose();
        argv.Dispose();
        return result;
    }

    public static GitResult Run(NativeStringView workDir, NativeStringView arg0, NativeStringView arg1, NativeStringView arg2)
    {
        var argv = new NativeList<NativeString>(6);
        argv.Add(NativeString.From("git"u8));
        argv.Add(NativeString.From("-C"u8));
        argv.Add(NativeString.From(workDir));
        argv.Add(NativeString.From(arg0));
        argv.Add(NativeString.From(arg1));
        argv.Add(NativeString.From(arg2));
        var result = PosixSpawn.Run("git"u8, argv.AsView());
        for (int i = 0; i < argv.Length; i++) argv[i].Dispose();
        argv.Dispose();
        return result;
    }

    public static GitResult Clone(NativeStringView url, NativeStringView target)
    {
        var parent = Fs.GetDirectoryName(target);
        if (!parent.IsEmpty) Fs.TryCreateDirectory(parent.AsView());
        parent.Dispose();
        var argv = new NativeList<NativeString>(4);
        argv.Add(NativeString.From("git"u8));
        argv.Add(NativeString.From("clone"u8));
        argv.Add(NativeString.From(url));
        argv.Add(NativeString.From(target));
        var result = PosixSpawn.Run("git"u8, argv.AsView());
        for (int i = 0; i < argv.Length; i++) argv[i].Dispose();
        argv.Dispose();
        return result;
    }

    public static GitResult StatusPorcelain(NativeStringView workDir)
        => Run(workDir, "status"u8, "--porcelain"u8);

    public static GitResult AddAll(NativeStringView workDir)
        => Run(workDir, "add"u8, "-A"u8);

    public static GitResult Commit(NativeStringView workDir, NativeStringView message)
        => Run(workDir, "commit"u8, "-m"u8, message);

    public static GitResult Fetch(NativeStringView workDir)
        => Run(workDir, "fetch"u8, "origin"u8);

    public static GitResult Rebase(NativeStringView workDir, NativeStringView upstream)
        => Run(workDir, "rebase"u8, upstream);

    public static GitResult Push(NativeStringView workDir, NativeStringView branch)
        => Run(workDir, "push"u8, "origin"u8, branch);

    public static int RevListCount(NativeStringView workDir, NativeStringView range)
    {
        var r = Run(workDir, "rev-list"u8, "--count"u8, range);
        if (r.ExitCode != 0) { r.Dispose(); return 0; }
        int n = 0;
        var s = r.StdOut.AsView().Trim().Bytes;
        for (int i = 0; i < s.Length; i++)
        {
            byte b = s[i];
            if (b < (byte)'0' || b > (byte)'9') break;
            n = n * 10 + (b - (byte)'0');
        }
        r.Dispose();
        return n;
    }

    public struct DiffStat { public int Added; public int Deleted; }

    // Parses `--shortstat` output like " 3 files changed, 120 insertions(+), 40 deletions(-)".
    public static DiffStat ShortStat(NativeStringView workDir, NativeStringView range)
    {
        var r = Run(workDir, "diff"u8, "--shortstat"u8, range);
        if (r.ExitCode != 0) { r.Dispose(); return default; }
        var line = r.StdOut.AsView().Trim().Bytes;
        var stat = new DiffStat();
        int i = 0;
        while (i < line.Length)
        {
            while (i < line.Length && !IsDigit(line[i])) i++;
            int n = 0;
            while (i < line.Length && IsDigit(line[i])) { n = n * 10 + (line[i] - (byte)'0'); i++; }
            while (i < line.Length && line[i] == (byte)' ') i++;
            if (i < line.Length)
            {
                byte c = line[i];
                if (c == (byte)'i') stat.Added = n;
                else if (c == (byte)'d') stat.Deleted = n;
            }
            while (i < line.Length && line[i] != (byte)',') i++;
            if (i < line.Length) i++;
        }
        r.Dispose();
        return stat;
    }

    private static bool IsDigit(byte b) => b >= (byte)'0' && b <= (byte)'9';

    public static NativeString DefaultBranch(NativeStringView workDir)
    {
        var r = Run(workDir, "symbolic-ref"u8, "refs/remotes/origin/HEAD"u8);
        if (r.ExitCode != 0)
        {
            r.Dispose();
            return NativeString.From("main"u8);
        }
        var line = r.StdOut.AsView().Trim();
        int slash = line.LastIndexOf((byte)'/');
        var branch = NativeString.From(slash >= 0 ? line.Slice(slash + 1) : line);
        r.Dispose();
        return branch;
    }

    public static bool RebaseInProgress(NativeStringView workDir)
    {
        var dotGit = Fs.Combine(workDir, ".git"u8);
        var rebaseMerge = Fs.Combine(dotGit.AsView(), "rebase-merge"u8);
        var rebaseApply = Fs.Combine(dotGit.AsView(), "rebase-apply"u8);
        bool result = Fs.IsDirectory(rebaseMerge.AsView()) || Fs.IsDirectory(rebaseApply.AsView());
        dotGit.Dispose();
        rebaseMerge.Dispose();
        rebaseApply.Dispose();
        return result;
    }

    /// <summary>
    /// Derives a filesystem-safe clone directory name from a repo spec.
    /// Joins all path segments with '_' and replaces any char outside
    /// [A-Za-z0-9._-] with '_' (covers ':' in host:port and non-ASCII hosts).
    /// </summary>
    public static NativeString DeriveCloneName(NativeStringView spec)
    {
        var bytes = spec.Bytes;
        while (!bytes.IsEmpty && bytes[^1] == (byte)'/') bytes = bytes[..^1];
        if (new NativeStringView(bytes).EndsWith(".git"u8)) bytes = bytes[..^4];
        var buf = new NativeBuffer(bytes.Length);
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            bool safe =
                (b >= (byte)'A' && b <= (byte)'Z')
                || (b >= (byte)'a' && b <= (byte)'z')
                || (b >= (byte)'0' && b <= (byte)'9')
                || b == (byte)'.' || b == (byte)'-' || b == (byte)'_';
            buf.AppendByte(safe ? b : (byte)'_');
        }
        return buf.Freeze();
    }

    /// <summary>
    /// Builds an https clone URL from a repo spec. Two-segment specs
    /// (owner/repo) resolve to github.com; three-segment specs
    /// (host[:port]/owner/repo) use the given host verbatim.
    /// </summary>
    public static NativeString BuildCloneUrl(NativeStringView spec)
    {
        int firstSlash = spec.Bytes.IndexOf((byte)'/');
        int lastSlash = spec.Bytes.LastIndexOf((byte)'/');
        var buf = new NativeBuffer(spec.Length + 24);
        buf.Append("https://"u8);
        if (firstSlash == lastSlash) buf.Append("github.com/"u8);
        buf.Append(spec);
        return buf.Freeze();
    }
}
