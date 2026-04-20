using System.Runtime.InteropServices;
using Dotai.Native;
using Dotai.Ui;

namespace Dotai.Services;

// Thin wrapper around posix_spawnp that captures stdout and stderr.
// macOS only (see Libc.cs for the portability note).
// Uses temp files instead of pipes — no threads, no poll.
internal static unsafe class PosixSpawn
{
    private static int _counter;

    internal static GitResult Run(
        NativeStringView exe, NativeListView<NativeString> argv, NativeStringView cwd = default)
    {
        var argCount = argv.Length;

        // Build two temp paths: /tmp/dotai-<pid>-<counter>.out/.err
        var pid = Libc.Getpid();
        var counter = _counter++;
        var stdoutPath = BuildTempPath(pid, counter, ".out"u8);
        var stderrPath = BuildTempPath(pid, counter, ".err"u8);

        // File actions: open stdout and stderr temp files in child
        if (Libc.FileActionsInit(out var actions) != 0)
        {
            Fatal.Die("posix_spawn_file_actions_init failed"u8);
        }

        bool spawnOk = false;
        int childPid = -1;
        try
        {
            // Null-terminate temp paths for file actions
            byte* outStackBuf = stackalloc byte[256];
            byte* errStackBuf = stackalloc byte[256];
            byte* outPath = NullTerm(stdoutPath.AsView(), outStackBuf);
            byte* errPath = NullTerm(stderrPath.AsView(), errStackBuf);
            try
            {
                int openFlags = Libc.O_WRONLY | Libc.O_CREAT | Libc.O_TRUNC;
                Libc.FileActionsAddOpen(ref actions, 1, outPath, openFlags, 0x1A4 /* 0644 */);
                Libc.FileActionsAddOpen(ref actions, 2, errPath, openFlags, 0x1A4 /* 0644 */);
            }
            finally
            {
                if (outPath != outStackBuf) NativeMemory.Free(outPath);
                if (errPath != errStackBuf) NativeMemory.Free(errPath);
            }

            // Null-terminate executable
            byte* exeStackBuf = stackalloc byte[256];
            byte* exePtr = NullTerm(exe, exeStackBuf);
            try
            {
                // Build argv pointer array: [exe, arg0, arg1, ..., null]
                // Each arg needs null-termination. Allocate NativeBuffers per arg.
                var argPtrs = (byte**)NativeMemory.Alloc((nuint)((argCount + 1) * sizeof(byte*)));
                var argBufs = (byte**)NativeMemory.Alloc((nuint)(argCount * sizeof(byte*)));
                try
                {
                    for (int i = 0; i < argCount; i++)
                    {
                        var v = argv[i].AsView();
                        byte* ab = (byte*)NativeMemory.Alloc((nuint)(v.Length + 1));
                        v.Bytes.CopyTo(new Span<byte>(ab, v.Length));
                        ab[v.Length] = 0;
                        argPtrs[i] = ab;
                        argBufs[i] = ab;
                    }
                    argPtrs[argCount] = null;

                    int ret = Libc.PosixSpawnp(out childPid, exePtr, ref actions, IntPtr.Zero, argPtrs, null);
                    if (ret != 0)
                    {
                        var buf = new NativeBuffer(48);
                        buf.Append("posix_spawnp failed errno "u8);
                        buf.AppendInt(ret);
                        Fatal.Die(buf.AsView());
                    }
                    spawnOk = true;
                }
                finally
                {
                    for (int i = 0; i < argCount; i++)
                        NativeMemory.Free(argBufs[i]);
                    NativeMemory.Free(argPtrs);
                    NativeMemory.Free(argBufs);
                }
            }
            finally
            {
                if (exePtr != exeStackBuf) NativeMemory.Free(exePtr);
            }
        }
        finally
        {
            Libc.FileActionsDestroy(ref actions);
        }

        if (!spawnOk)
        {
            stdoutPath.Dispose();
            stderrPath.Dispose();
            return default;
        }

        // Wait for child to finish
        Libc.Waitpid(childPid, out var wstatus, 0);
        var exitCode = (wstatus >> 8) & 0xFF;

        // Read temp files
        var stdoutNs = TryReadAllBytes(stdoutPath.AsView());
        var stderrNs = TryReadAllBytes(stderrPath.AsView());

        // Clean up
        TryDeleteFile(stdoutPath.AsView());
        TryDeleteFile(stderrPath.AsView());
        stdoutPath.Dispose();
        stderrPath.Dispose();

        return new GitResult(exitCode, stdoutNs, stderrNs);
    }

    private static NativeString TryReadAllBytes(NativeStringView path)
    {
        byte* buf = stackalloc byte[256];
        byte* p = NullTerm(path, buf);
        int fd = Libc.Open(p, Libc.O_RDONLY, 0);
        if (p != buf) NativeMemory.Free(p);
        if (fd < 0) return default;
        try
        {
            byte* statbuf = stackalloc byte[Libc.StatBufSize];
            if (Libc.FStat(fd, statbuf) != 0) return default;
            var size = (int)BitConverter.ToInt64(new ReadOnlySpan<byte>(statbuf, Libc.StatBufSize).Slice(96, 8));
            if (size == 0) return default;
            byte* data = (byte*)NativeMemory.Alloc((nuint)size);
            long total = 0;
            while (total < size)
            {
                var n = Libc.Read(fd, data + total, (nuint)(size - total));
                if (n <= 0) break;
                total += n;
            }
            return NativeString.Wrap(data, (int)total);
        }
        finally { Libc.Close(fd); }
    }

    private static void TryDeleteFile(NativeStringView path)
    {
        byte* buf = stackalloc byte[256];
        byte* p = NullTerm(path, buf);
        Libc.Unlink(p);
        if (p != buf) NativeMemory.Free(p);
    }

    // Builds: /tmp/dotai-<pid>-<counter><suffix> (as NativeString, caller disposes)
    private static NativeString BuildTempPath(int pid, int counter, ReadOnlySpan<byte> suffix)
    {
        ReadOnlySpan<byte> prefix = "/tmp/dotai-"u8;
        var buf = new NativeBuffer(prefix.Length + 22 + suffix.Length);
        buf.Append(new NativeStringView(prefix));
        buf.AppendInt(pid);
        buf.AppendByte((byte)'-');
        buf.AppendInt(counter);
        buf.Append(new NativeStringView(suffix));
        return buf.Freeze();
    }

    private static byte* NullTerm(NativeStringView path, byte* stackBuf)
    {
        if (path.Length < 256)
        {
            path.Bytes.CopyTo(new Span<byte>(stackBuf, path.Length));
            stackBuf[path.Length] = 0;
            return stackBuf;
        }
        byte* p = (byte*)NativeMemory.Alloc((nuint)(path.Length + 1));
        path.Bytes.CopyTo(new Span<byte>(p, path.Length));
        p[path.Length] = 0;
        return p;
    }
}
