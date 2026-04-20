using System.Runtime.InteropServices;
using Dotai.Text;
using Dotai.Ui;

namespace Dotai.Services;

// Thin wrapper around posix_spawnp that captures stdout and stderr.
// macOS only (see Libc.cs for the portability note).
// Uses temp files instead of pipes — no threads, no poll.
internal static unsafe class PosixSpawn
{
    private static int _counter;

    // executableNullTerminated must be a null-terminated UTF-8 byte array.
    internal static (int exitCode, byte[] stdout, byte[] stderr) Run(
        byte[] executableNullTerminated, byte[][] argv)
    {
        var argCount = argv.Length;
        var argBytes = new byte[argCount][];
        for (var i = 0; i < argCount; i++)
            argBytes[i] = NullTerminate(argv[i]);

        var execBytes = executableNullTerminated;

        // Build two temp paths: /tmp/dotai-<pid>-<counter>.out/.err
        var pid = Libc.Getpid();
        var counter = _counter++;
        var stdoutPath = BuildTempPath(pid, counter, ".out\0"u8);
        var stderrPath = BuildTempPath(pid, counter, ".err\0"u8);

        // File actions: open stdout and stderr temp files in child
        if (Libc.FileActionsInit(out var actions) != 0)
        {
            ConsoleOut.Error("posix_spawn_file_actions_init failed"u8);
            return (-1, [], []);
        }

        bool spawnOk = false;
        int childPid = -1;
        try
        {
            fixed (byte* outPath = stdoutPath)
            fixed (byte* errPath = stderrPath)
            {
                int openFlags = Libc.O_WRONLY | Libc.O_CREAT | Libc.O_TRUNC;
                Libc.FileActionsAddOpen(ref actions, 1, outPath, openFlags, 0x1A4 /* 0644 */);
                Libc.FileActionsAddOpen(ref actions, 2, errPath, openFlags, 0x1A4 /* 0644 */);
            }

            // Pin all argv byte arrays and build the argv pointer array.
            var handles = new GCHandle[argCount];
            var argPtrs = new byte*[argCount + 1]; // +1 for null terminator
            try
            {
                for (var i = 0; i < argCount; i++)
                {
                    handles[i] = GCHandle.Alloc(argBytes[i], GCHandleType.Pinned);
                    argPtrs[i] = (byte*)handles[i].AddrOfPinnedObject();
                }
                argPtrs[argCount] = null;

                fixed (byte* execPtr = execBytes)
                fixed (byte** argvPtr = argPtrs)
                {
                    int ret = Libc.PosixSpawnp(out childPid, execPtr, ref actions, IntPtr.Zero, argvPtr, null);
                    if (ret != 0)
                    {
                        var buf = new ByteBuffer(48);
                        buf.Append("posix_spawnp failed errno "u8);
                        buf.AppendInt(ret);
                        ConsoleOut.Error(buf.Span);
                        return (-1, [], []);
                    }
                    spawnOk = true;
                }
            }
            finally
            {
                for (var i = 0; i < argCount; i++)
                    if (handles[i].IsAllocated) handles[i].Free();
            }
        }
        finally
        {
            Libc.FileActionsDestroy(ref actions);
        }

        if (!spawnOk) return (-1, [], []);

        // Wait for child to finish
        Libc.Waitpid(childPid, out var wstatus, 0);
        var exitCode = (wstatus >> 8) & 0xFF;

        // Read temp files
        FastString outFs = stdoutPath[..^1]; // strip null terminator for FastString
        FastString errFs = stderrPath[..^1];
        var stdoutBytes = TryReadAllBytes(outFs);
        var stderrBytes = TryReadAllBytes(errFs);

        // Clean up
        TryDeleteFile(outFs);
        TryDeleteFile(errFs);

        return (exitCode, stdoutBytes, stderrBytes);
    }

    private static byte[] TryReadAllBytes(FastString path)
    {
        var buf = Fs.NullTerminate(path);
        int fd;
        fixed (byte* p = buf) fd = Libc.Open(p, Libc.O_RDONLY, 0);
        if (fd < 0) return [];
        try
        {
            var statbuf = stackalloc byte[Libc.StatBufSize];
            if (Libc.FStat(fd, statbuf) != 0) return [];
            var size = (int)BitConverter.ToInt64(new ReadOnlySpan<byte>(statbuf, Libc.StatBufSize).Slice(96, 8));
            if (size == 0) return [];
            var data = new byte[size];
            fixed (byte* dp = data)
            {
                long total = 0;
                while (total < size)
                {
                    var n = Libc.Read(fd, dp + total, (nuint)(size - total));
                    if (n <= 0) break;
                    total += n;
                }
                if (total < size) Array.Resize(ref data, (int)total);
            }
            return data;
        }
        finally { Libc.Close(fd); }
    }

    private static void TryDeleteFile(FastString path)
    {
        var buf = Fs.NullTerminate(path);
        fixed (byte* p = buf) Libc.Unlink(p);
    }

    // Builds: /tmp/dotai-<pid>-<counter><suffix\0>
    // suffix must be null-terminated (e.g. ".out\0"u8)
    private static byte[] BuildTempPath(int pid, int counter, ReadOnlySpan<byte> suffix)
    {
        // prefix: "/tmp/dotai-"
        ReadOnlySpan<byte> prefix = "/tmp/dotai-"u8;
        // max digits for int: 10 digits + '-' + 10 digits = 21 chars, plus suffix
        var buf = new byte[prefix.Length + 21 + suffix.Length];
        int pos = 0;
        prefix.CopyTo(buf);
        pos += prefix.Length;
        pos += WriteInt(buf, pos, pid);
        buf[pos++] = (byte)'-';
        pos += WriteInt(buf, pos, counter);
        suffix.CopyTo(buf.AsSpan(pos));
        pos += suffix.Length;
        return buf[..pos];
    }

    private static int WriteInt(byte[] buf, int offset, int value)
    {
        if (value == 0) { buf[offset] = (byte)'0'; return 1; }
        // Write digits in reverse
        int start = offset;
        int v = value < 0 ? -value : value;
        int end = offset;
        while (v > 0) { buf[end++] = (byte)('0' + v % 10); v /= 10; }
        // Reverse
        int lo = start, hi = end - 1;
        while (lo < hi) { (buf[lo], buf[hi]) = (buf[hi], buf[lo]); lo++; hi--; }
        return end - start;
    }

    // Appends a null byte to an already-UTF-8 byte array.
    private static byte[] NullTerminate(byte[] s)
    {
        var buf = new byte[s.Length + 1];
        s.CopyTo(buf, 0);
        return buf;
    }
}
