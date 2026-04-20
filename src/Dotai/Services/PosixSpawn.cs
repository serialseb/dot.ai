using System.Runtime.InteropServices;

namespace Dotai.Services;

// Thin wrapper around posix_spawnp that captures stdout and stderr.
// macOS only (see Libc.cs for the portability note).
internal static unsafe class PosixSpawn
{
    // executableNullTerminated must be a null-terminated UTF-8 byte array
    // (e.g. "git\0"u8.ToArray() or NullTerminate(someBytes)).
    internal static (int exitCode, byte[] stdout, byte[] stderr) Run(
        byte[] executableNullTerminated, byte[][] argv)
    {
        var argCount = argv.Length;
        var argBytes = new byte[argCount][];
        for (var i = 0; i < argCount; i++)
            argBytes[i] = NullTerminate(argv[i]);

        var execBytes = executableNullTerminated;

        var stdoutFds = stackalloc int[2];
        var stderrFds = stackalloc int[2];

        if (Libc.Pipe(stdoutFds) != 0) throw new InvalidOperationException("pipe() failed for stdout");
        if (Libc.Pipe(stderrFds) != 0)
        {
            Libc.Close(stdoutFds[0]);
            Libc.Close(stdoutFds[1]);
            throw new InvalidOperationException("pipe() failed for stderr");
        }

        Libc.FileActionsInit(out var actions);
        try
        {
            // dup2 the write ends into fd 1 (stdout) and fd 2 (stderr) in the child.
            Libc.FileActionsAddDup2(ref actions, stdoutFds[1], 1);
            Libc.FileActionsAddDup2(ref actions, stderrFds[1], 2);
            // Close read ends in the child — the child should not inherit them.
            Libc.FileActionsAddClose(ref actions, stdoutFds[0]);
            Libc.FileActionsAddClose(ref actions, stderrFds[0]);

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

                int pid;
                fixed (byte* execPtr = execBytes)
                fixed (byte** argvPtr = argPtrs)
                {
                    int ret = Libc.PosixSpawnp(out pid, execPtr, ref actions, IntPtr.Zero, argvPtr, null);
                    if (ret != 0)
                        throw new InvalidOperationException($"posix_spawnp failed with errno {ret}");
                }

                // Close write ends in parent so we see EOF when child exits.
                Libc.Close(stdoutFds[1]);
                Libc.Close(stderrFds[1]);

                // Read both pipes concurrently on background threads.
                byte[] stdoutBytes = [];
                byte[] stderrBytes = [];
                var t1 = new Thread(() => stdoutBytes = DrainFd(stdoutFds[0])) { IsBackground = true };
                var t2 = new Thread(() => stderrBytes = DrainFd(stderrFds[0])) { IsBackground = true };
                t1.Start();
                t2.Start();
                t1.Join();
                t2.Join();

                Libc.Waitpid(pid, out var wstatus, 0);
                // WEXITSTATUS: bits 8–15 of wstatus (WIFEXITED is assumed).
                var exitCode = (wstatus >> 8) & 0xFF;

                return (exitCode, stdoutBytes, stderrBytes);
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
    }

    private static byte[] DrainFd(int fd)
    {
        var ms = new MemoryStream();
        var buf = new byte[4096];
        long n;
        fixed (byte* p = buf)
            while ((n = Libc.Read(fd, p, (nuint)buf.Length)) > 0)
                ms.Write(buf, 0, (int)n);
        Libc.Close(fd);
        return ms.ToArray();
    }

    // Appends a null byte to an already-UTF-8 byte array.
    private static byte[] NullTerminate(byte[] s)
    {
        var buf = new byte[s.Length + 1];
        s.CopyTo(buf, 0);
        return buf;
    }

}
