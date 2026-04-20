using Dotai.Text;
using Dotai.Ui;

namespace Dotai.Services;

// libc-backed filesystem helpers. All paths are null-terminated UTF-8 byte buffers.
// macOS only (see Libc.cs for the portability note).
// All mutating operations return bool; on failure they emit ConsoleOut.Error and return false.
public static unsafe class Fs
{
    // ── existence / type ────────────────────────────────────────────────────

    public static bool Exists(FastString path)
    {
        var buf = NullTerminate(path);
        int ret;
        fixed (byte* p = buf) ret = Libc.Access(p, Libc.F_OK);
        return ret == 0;
    }

    public static bool IsDirectory(FastString path)
    {
        var statbuf = stackalloc byte[Libc.StatBufSize];
        var buf = NullTerminate(path);
        int ret;
        fixed (byte* p = buf) ret = Libc.LStat(p, statbuf);
        if (ret != 0) return false;
        var mode = Libc.ReadMode(new ReadOnlySpan<byte>(statbuf, Libc.StatBufSize));
        return (mode & Libc.S_IFMT) == Libc.S_IFDIR;
    }

    public static bool IsSymlink(FastString path)
    {
        var statbuf = stackalloc byte[Libc.StatBufSize];
        var buf = NullTerminate(path);
        int ret;
        fixed (byte* p = buf) ret = Libc.LStat(p, statbuf);
        if (ret != 0) return false;
        var mode = Libc.ReadMode(new ReadOnlySpan<byte>(statbuf, Libc.StatBufSize));
        return (mode & Libc.S_IFMT) == Libc.S_IFLNK;
    }

    // ── read / write ────────────────────────────────────────────────────────

    public static bool TryReadAllBytes(FastString path, out byte[] bytes)
    {
        var buf = NullTerminate(path);
        int fd;
        fixed (byte* p = buf) fd = Libc.Open(p, Libc.O_RDONLY, 0);
        if (fd < 0)
        {
            EmitError("open failed: "u8, path);
            bytes = [];
            return false;
        }
        try
        {
            var statbuf = stackalloc byte[Libc.StatBufSize];
            if (Libc.FStat(fd, statbuf) != 0)
            {
                EmitError("fstat failed: "u8, path);
                bytes = [];
                return false;
            }
            var size = (int)BitConverter.ToInt64(new ReadOnlySpan<byte>(statbuf, Libc.StatBufSize).Slice(96, 8));
            if (size == 0) { bytes = []; return true; }
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
            bytes = data;
            return true;
        }
        finally { Libc.Close(fd); }
    }

    public static bool TryWriteAllBytes(FastString path, ReadOnlySpan<byte> data)
    {
        var pathBuf = NullTerminate(path);
        int fd;
        fixed (byte* p = pathBuf) fd = Libc.Creat(p, 0x1A4 /* 0644 */);
        if (fd < 0)
        {
            EmitError("creat failed: "u8, path);
            return false;
        }
        try
        {
            fixed (byte* dp = data)
            {
                long total = 0;
                while (total < data.Length)
                {
                    var n = Libc.Write(fd, dp + total, (nuint)(data.Length - total));
                    if (n <= 0)
                    {
                        EmitError("write failed: "u8, path);
                        return false;
                    }
                    total += n;
                }
            }
            return true;
        }
        finally { Libc.Close(fd); }
    }

    // ── directory creation ───────────────────────────────────────────────────

    public static bool TryCreateDirectory(FastString path)
    {
        var bytes = path.Bytes;
        var buf = new byte[bytes.Length + 1];
        bytes.CopyTo(buf);

        for (int i = 1; i <= bytes.Length; i++)
        {
            bool isLeaf = i == bytes.Length;
            bool isSep  = !isLeaf && bytes[i] == (byte)'/';
            if (!isLeaf && !isSep) continue;

            var saved = buf[i];
            buf[i] = 0;
            fixed (byte* p = buf)
            {
                var r = Libc.Mkdir(p, 0x1ED /* 0755 */);
                if (r != 0 && Libc.Errno() != 17 /* EEXIST */ && isLeaf)
                {
                    EmitError("mkdir failed: "u8, path);
                    return false;
                }
            }
            buf[i] = saved;
        }
        return true;
    }

    // ── symlinks ─────────────────────────────────────────────────────────────

    public static bool TryCreateSymbolicLink(FastString link, FastString target)
    {
        var targetBuf = NullTerminate(target);
        var linkBuf = NullTerminate(link);
        fixed (byte* t = targetBuf)
        fixed (byte* l = linkBuf)
        {
            if (Libc.Symlink(t, l) != 0)
            {
                EmitError("symlink failed: "u8, link);
                return false;
            }
        }
        return true;
    }

    public static byte[]? ReadSymbolicLinkTarget(FastString link)
    {
        if (!IsSymlink(link)) return null;
        var linkBuf = NullTerminate(link);
        var outBuf = new byte[256];
        long len;
        fixed (byte* lp = linkBuf)
        fixed (byte* p = outBuf)
            len = Libc.ReadLink(lp, p, (nuint)outBuf.Length);
        if (len < 0) return null;
        if (len == outBuf.Length)
        {
            outBuf = new byte[4096];
            fixed (byte* lp = linkBuf)
            fixed (byte* p = outBuf)
                len = Libc.ReadLink(lp, p, (nuint)outBuf.Length);
            if (len < 0) return null;
        }
        return outBuf[..(int)len];
    }

    // ── delete ───────────────────────────────────────────────────────────────

    public static bool TryDeleteFile(FastString path)
    {
        var buf = NullTerminate(path);
        int ret;
        fixed (byte* p = buf) ret = Libc.Unlink(p);
        if (ret != 0)
        {
            EmitError("unlink failed: "u8, path);
            return false;
        }
        return true;
    }

    // ── path manipulation ────────────────────────────────────────────────────

    public static bool TryGetCurrentDirectory(out byte[] cwd)
    {
        var buf = new byte[4096];
        fixed (byte* p = buf)
        {
            var ptr = Libc.Getcwd(p, (nuint)buf.Length);
            if (ptr == IntPtr.Zero)
            {
                ConsoleOut.Error("getcwd failed"u8);
                cwd = [];
                return false;
            }
            cwd = SliceToNull(buf);
            return true;
        }
    }

    public static byte[] GetFullPath(FastString path)
    {
        if (path.IsEmpty)
        {
            TryGetCurrentDirectory(out var cwd2);
            return cwd2;
        }
        if (path.Bytes[0] == (byte)'/') return NormalizePath(path.Bytes);
        TryGetCurrentDirectory(out var cwd);
        var combined = Combine(cwd, path);
        return NormalizePath(combined);
    }

    // Resolve . and .. segments in an absolute path without syscalls.
    private static byte[] NormalizePath(ReadOnlySpan<byte> abs)
    {
        var parts = new List<byte[]>();
        var span = abs;
        if (!span.IsEmpty && span[0] == (byte)'/') span = span[1..];
        while (!span.IsEmpty)
        {
            int sep = span.IndexOf((byte)'/');
            var seg = sep < 0 ? span : span[..sep];
            if (seg.IsEmpty || seg.SequenceEqual("."u8))
            {
                // skip
            }
            else if (seg.SequenceEqual(".."u8))
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
            }
            else
            {
                parts.Add(seg.ToArray());
            }
            if (sep < 0) break;
            span = span[(sep + 1)..];
        }
        if (parts.Count == 0) return "/"u8.ToArray();
        int totalLen = 1; // leading '/'
        for (int i = 0; i < parts.Count; i++) totalLen += (i > 0 ? 1 : 0) + parts[i].Length;
        var result = new byte[totalLen];
        int pos = 0;
        result[pos++] = (byte)'/';
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0) result[pos++] = (byte)'/';
            parts[i].CopyTo(result.AsSpan(pos));
            pos += parts[i].Length;
        }
        return result[..pos];
    }

    public static byte[] GetDirectoryName(FastString path)
    {
        var bytes = path.Bytes;
        while (!bytes.IsEmpty && bytes[^1] == (byte)'/') bytes = bytes[..^1];
        int slash = bytes.LastIndexOf((byte)'/');
        if (slash < 0) return "."u8.ToArray();
        if (slash == 0) return "/"u8.ToArray();
        return bytes[..slash].ToArray();
    }

    public static byte[] GetFileName(FastString path)
    {
        var bytes = path.Bytes;
        while (!bytes.IsEmpty && bytes[^1] == (byte)'/') bytes = bytes[..^1];
        int slash = bytes.LastIndexOf((byte)'/');
        return slash < 0 ? bytes.ToArray() : bytes[(slash + 1)..].ToArray();
    }

    public static byte[] Combine(FastString a, FastString b)
    {
        var ab = a.Bytes;
        var bb = b.Bytes;
        if (bb.IsEmpty) return ab.ToArray();
        if (ab.IsEmpty) return bb.ToArray();
        if (bb[0] == (byte)'/') return bb.ToArray();
        while (!ab.IsEmpty && ab[^1] == (byte)'/') ab = ab[..^1];
        var result = new byte[ab.Length + 1 + bb.Length];
        ab.CopyTo(result);
        result[ab.Length] = (byte)'/';
        bb.CopyTo(result.AsSpan(ab.Length + 1));
        return result;
    }

    public static byte[] Combine(FastString a, FastString b, FastString c)
        => Combine(Combine(a, b), c);

    public static byte[] GetRelativePath(FastString from, FastString to)
    {
        var fb = from.Bytes;
        var tb = to.Bytes;
        while (!fb.IsEmpty && fb[^1] == (byte)'/') fb = fb[..^1];
        while (!tb.IsEmpty && tb[^1] == (byte)'/') tb = tb[..^1];

        if (fb.SequenceEqual(tb)) return "."u8.ToArray();

        int commonLen = 0;
        int minLen = Math.Min(fb.Length, tb.Length);
        for (int i = 0; i < minLen; i++)
        {
            if (fb[i] != tb[i]) break;
            if (fb[i] == (byte)'/') commonLen = i;
        }
        if (minLen > 0 && minLen <= fb.Length && minLen <= tb.Length)
        {
            if (fb.Length == minLen && tb.Length > minLen && tb[minLen] == (byte)'/') commonLen = minLen;
            else if (tb.Length == minLen && fb.Length > minLen && fb[minLen] == (byte)'/') commonLen = minLen;
        }

        ReadOnlySpan<byte> fromTail = commonLen == 0 ? fb
            : (commonLen < fb.Length ? fb[(commonLen + 1)..] : ReadOnlySpan<byte>.Empty);
        int hops = 0;
        if (!fromTail.IsEmpty)
        {
            hops = 1;
            for (int i = 0; i < fromTail.Length; i++)
                if (fromTail[i] == (byte)'/') hops++;
        }

        ReadOnlySpan<byte> toTail = commonLen == 0 ? tb
            : (commonLen < tb.Length ? tb[(commonLen + 1)..] : ReadOnlySpan<byte>.Empty);

        var totalLen = hops * 3 + toTail.Length;
        if (totalLen == 0) return "."u8.ToArray();
        var result = new byte[totalLen];
        int pos = 0;
        for (int i = 0; i < hops; i++) { result[pos++] = (byte)'.'; result[pos++] = (byte)'.'; result[pos++] = (byte)'/'; }
        toTail.CopyTo(result.AsSpan(pos));
        int end = result.Length;
        while (end > 0 && result[end - 1] == (byte)'/') end--;
        return end == 0 ? "."u8.ToArray() : result[..end];
    }

    // ── enumeration ──────────────────────────────────────────────────────────

    public static IEnumerable<byte[]> EnumerateDirectories(FastString path)
        => CollectEntries(path.Bytes.ToArray(), Libc.DT_DIR);

    public static IEnumerable<byte[]> EnumerateFiles(FastString path, bool recursive = false)
    {
        var files = CollectEntries(path.Bytes.ToArray(), Libc.DT_REG);
        if (!recursive) return files;
        var all = new List<byte[]>(files);
        var dirs = CollectEntries(path.Bytes.ToArray(), Libc.DT_DIR);
        for (int i = 0; i < dirs.Count; i++)
            all.AddRange(EnumerateFiles(dirs[i], recursive: true));
        return all;
    }

    public static IEnumerable<byte[]> EnumerateFileSystemEntries(FastString path)
        => CollectEntries(path.Bytes.ToArray(), 0 /* all types */);

    private static List<byte[]> CollectEntries(byte[] pathBytes, byte typeFilter)
    {
        var result = new List<byte[]>();
        var pathBuf = new byte[pathBytes.Length + 1];
        pathBytes.CopyTo(pathBuf, 0);

        IntPtr dirp;
        unsafe
        {
            fixed (byte* p = pathBuf) dirp = Libc.Opendir(p);
        }
        if (dirp == IntPtr.Zero) return result;

        try
        {
            unsafe
            {
                while (true)
                {
                    var dirent = Libc.Readdir(dirp);
                    if (dirent == IntPtr.Zero) break;

                    var namlen  = *(ushort*)((byte*)dirent + Libc.DirentNamlenOff);
                    var dtype   = *((byte*)dirent + Libc.DirentTypeOff);
                    var namePtr = (byte*)dirent + Libc.DirentNameOff;

                    if (namlen == 1 && *namePtr == (byte)'.') continue;
                    if (namlen == 2 && *namePtr == (byte)'.' && *(namePtr + 1) == (byte)'.') continue;
                    if (typeFilter != 0 && dtype != typeFilter) continue;

                    var name = new byte[namlen];
                    new ReadOnlySpan<byte>(namePtr, namlen).CopyTo(name);
                    result.Add(Combine(pathBytes, name));
                }
            }
        }
        finally { Libc.Closedir(dirp); }
        return result;
    }

    // ── internal helpers ─────────────────────────────────────────────────────

    internal static byte[] NullTerminate(FastString path)
    {
        var bytes = path.Bytes;
        var buf = new byte[bytes.Length + 1];
        bytes.CopyTo(buf);
        return buf;
    }

    private static byte[] SliceToNull(byte[] buf)
    {
        int len = 0;
        while (len < buf.Length && buf[len] != 0) len++;
        return buf[..len];
    }

    private static void EmitError(ReadOnlySpan<byte> prefix, FastString path)
    {
        var b = new ByteBuffer(prefix.Length + path.Bytes.Length);
        b.Append(prefix);
        b.Append(path.Bytes);
        ConsoleOut.Error(b.Span);
    }
}
