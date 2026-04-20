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

    [LibraryImport("libc", EntryPoint = "close")]
    internal static partial int Close(int fd);

    [LibraryImport("libc", EntryPoint = "read")]
    internal static partial long Read(int fd, byte* buf, nuint count);

    [LibraryImport("libc", EntryPoint = "waitpid")]
    internal static partial int Waitpid(int pid, out int status, int options);

    [LibraryImport("libc", EntryPoint = "write")]
    internal static partial long Write(int fd, byte* buf, nuint count);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_addopen")]
    internal static partial int FileActionsAddOpen(ref IntPtr actions, int fd, byte* path, int oflag, int mode);

    [LibraryImport("libc", EntryPoint = "getpid")]
    internal static partial int Getpid();

    [LibraryImport("libc", EntryPoint = "nanosleep")]
    internal static partial int Nanosleep(byte* req, byte* rem);

    [LibraryImport("libc", EntryPoint = "isatty")]
    internal static partial int IsAtty(int fd);

    // --- filesystem primitives ---

    [LibraryImport("libc", EntryPoint = "open")]
    internal static partial int Open(byte* path, int flags, int mode);

    // creat(2) is equivalent to open(O_WRONLY|O_CREAT|O_TRUNC, mode) but with
    // a fixed (non-variadic) signature, which avoids Apple ARM64 ABI issues with
    // passing the mode through the variadic slot of open(2).
    [LibraryImport("libc", EntryPoint = "creat")]
    internal static partial int Creat(byte* path, int mode);

    [LibraryImport("libc", EntryPoint = "mkdir")]
    internal static partial int Mkdir(byte* path, int mode);

    [LibraryImport("libc", EntryPoint = "access")]
    internal static partial int Access(byte* path, int mode);

    [LibraryImport("libc", EntryPoint = "lstat")]
    internal static partial int LStat(byte* path, byte* statbuf);

    [LibraryImport("libc", EntryPoint = "stat")]
    internal static partial int Stat(byte* path, byte* statbuf);

    [LibraryImport("libc", EntryPoint = "symlink")]
    internal static partial int Symlink(byte* target, byte* linkpath);

    [LibraryImport("libc", EntryPoint = "readlink")]
    internal static partial long ReadLink(byte* path, byte* buf, nuint bufsize);

    [LibraryImport("libc", EntryPoint = "unlink")]
    internal static partial int Unlink(byte* path);

    [LibraryImport("libc", EntryPoint = "opendir")]
    internal static partial IntPtr Opendir(byte* path);

    [LibraryImport("libc", EntryPoint = "readdir")]
    internal static partial IntPtr Readdir(IntPtr dirp);

    [LibraryImport("libc", EntryPoint = "closedir")]
    internal static partial int Closedir(IntPtr dirp);

    [LibraryImport("libc", EntryPoint = "__error")]
    internal static partial int* ErrnoPtr();

    [LibraryImport("libc", EntryPoint = "fstat")]
    internal static partial int FStat(int fd, byte* statbuf);

    [LibraryImport("libc", EntryPoint = "getcwd")]
    internal static partial IntPtr Getcwd(byte* buf, nuint size);

    // macOS struct stat is 144 bytes (_DARWIN_USE_64_BIT_INODE).
    // st_mode (mode_t / uint16) sits at byte offset 4.
    internal const int StatBufSize = 144;

    internal static uint ReadMode(ReadOnlySpan<byte> statbuf)
        => (uint)BitConverter.ToUInt16(statbuf.Slice(4, 2));

    // open flags (macOS values)
    internal const int O_RDONLY = 0;
    internal const int O_WRONLY = 1;
    internal const int O_CREAT  = 0x200;
    internal const int O_TRUNC  = 0x400;

    // mode bits
    internal const uint S_IFMT  = 0xF000; // 0o170000
    internal const uint S_IFLNK = 0xA000; // 0o120000
    internal const uint S_IFREG = 0x8000; // 0o100000
    internal const uint S_IFDIR = 0x4000; // 0o040000

    // access(2) flags
    internal const int F_OK = 0;

    // dirent d_type values
    internal const byte DT_DIR = 4;
    internal const byte DT_REG = 8;
    internal const byte DT_LNK = 10;

    // macOS dirent offsets (_DARWIN_USE_64_BIT_INODE)
    // d_ino(8) + d_seekoff(8) + d_reclen(2) + d_namlen(2) + d_type(1) + d_name(1024)
    internal const int DirentReclenOff = 16;
    internal const int DirentNamlenOff = 18;
    internal const int DirentTypeOff   = 20;
    internal const int DirentNameOff   = 21;

    internal static int Errno() => *ErrnoPtr();
}
