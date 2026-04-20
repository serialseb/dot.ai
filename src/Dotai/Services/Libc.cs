using System.Runtime.InteropServices;

namespace Dotai.Services;

// macOS-only: posix_spawn_file_actions_t is typedef void* (a single pointer).
// All posix_spawn_file_actions_* functions take posix_spawn_file_actions_t*,
// which is void** — modelled as ref IntPtr in P/Invoke.
// posix_spawnp takes const posix_spawn_file_actions_t*, also void** — ref IntPtr.
// On Linux (glibc) posix_spawn_file_actions_t is a ~76-byte struct; a separate
// port is needed there.
internal static unsafe partial class Libc
{
    [LibraryImport("libc", EntryPoint = "posix_spawnp")]
    internal static partial int PosixSpawnp(
        out int pid,
        byte* path,
        ref IntPtr fileActions,
        IntPtr attrp,
        byte** argv,
        byte** envp);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_init")]
    internal static partial int FileActionsInit(out IntPtr actions);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_destroy")]
    internal static partial int FileActionsDestroy(ref IntPtr actions);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_adddup2")]
    internal static partial int FileActionsAddDup2(ref IntPtr actions, int fd, int newfd);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_addclose")]
    internal static partial int FileActionsAddClose(ref IntPtr actions, int fd);

    [LibraryImport("libc", EntryPoint = "pipe")]
    internal static partial int Pipe(int* fds);

    [LibraryImport("libc", EntryPoint = "close")]
    internal static partial int Close(int fd);

    [LibraryImport("libc", EntryPoint = "read")]
    internal static partial long Read(int fd, byte* buf, nuint count);

    [LibraryImport("libc", EntryPoint = "waitpid")]
    internal static partial int Waitpid(int pid, out int status, int options);
}
