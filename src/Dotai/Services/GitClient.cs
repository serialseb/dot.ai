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

    public static NativeString DefaultBranch(NativeStringView workDir)
    {
        var r = Run(workDir, "symbolic-ref"u8, "refs/remotes/origin/HEAD"u8);
        if (r.ExitCode != 0)
        {
            r.Dispose();
            return NativeString.From("main"u8);
        }
        var branch = NativeString.From(r.StdOut.AsView().GetDefaultBranchFromSymbolicRef());
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
    /// Derives a filesystem-safe clone directory name from a remote URL.
    /// </summary>
    public static NativeString DeriveCloneName(NativeStringView url)
    {
        var bytes = url.Bytes;
        while (!bytes.IsEmpty && bytes[^1] == (byte)'/') bytes = bytes[..^1];
        if (new NativeStringView(bytes).EndsWith(".git"u8)) bytes = bytes[..^4];
        int last = bytes.LastIndexOf((byte)'/');
        if (last < 0)
        {
            // Replace all '/' with '_'
            var buf = new NativeBuffer(bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
                buf.AppendByte(bytes[i] == (byte)'/' ? (byte)'_' : bytes[i]);
            return buf.Freeze();
        }
        int secondLast = bytes[..last].LastIndexOf((byte)'/');
        ReadOnlySpan<byte> seg1 = secondLast >= 0 ? bytes[(secondLast + 1)..last] : bytes[..last];
        ReadOnlySpan<byte> seg2 = bytes[(last + 1)..];
        var res = new NativeBuffer(seg1.Length + 1 + seg2.Length);
        res.Append(new NativeStringView(seg1));
        res.AppendByte((byte)'_');
        res.Append(new NativeStringView(seg2));
        return res.Freeze();
    }
}
