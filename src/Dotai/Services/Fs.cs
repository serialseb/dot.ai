using Dotai.Text;

namespace Dotai.Services;

// libc-backed filesystem helpers. All paths are null-terminated UTF-8 byte buffers.
// macOS only (see Libc.cs for the portability note).
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

    public static byte[] ReadAllBytes(FastString path)
    {
        var buf = NullTerminate(path);
        int fd;
        fixed (byte* p = buf) fd = Libc.Open(p, Libc.O_RDONLY, 0);
        if (fd < 0) throw new IOException($"open failed: errno {Libc.Errno()}");
        try
        {
            var statbuf = stackalloc byte[Libc.StatBufSize];
            if (Libc.FStat(fd, statbuf) != 0) throw new IOException($"fstat failed: errno {Libc.Errno()}");
            // st_size is at offset 96 on macOS (int64)
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

    public static void WriteAllBytes(FastString path, ReadOnlySpan<byte> data)
    {
        // Use creat(2) instead of open(O_WRONLY|O_CREAT|O_TRUNC, mode) to avoid
        // Apple ARM64 ABI issues with open's variadic mode parameter.
        var pathBuf = NullTerminate(path);
        int fd;
        fixed (byte* p = pathBuf) fd = Libc.Creat(p, 0x1A4 /* 0644 */);
        if (fd < 0) throw new IOException($"creat failed: errno {Libc.Errno()}");
        try
        {
            fixed (byte* dp = data)
            {
                long total = 0;
                while (total < data.Length)
                {
                    var n = Libc.Write(fd, dp + total, (nuint)(data.Length - total));
                    if (n <= 0) throw new IOException($"write failed: errno {Libc.Errno()}");
                    total += n;
                }
            }
        }
        finally { Libc.Close(fd); }
    }

    // ── directory creation ───────────────────────────────────────────────────

    public static void CreateDirectory(FastString path)
    {
        // Walk each prefix segment and mkdir, ignoring EEXIST (17 on macOS).
        var bytes = path.Bytes;
        // buf is the full path; we null-terminate at each segment boundary temporarily.
        var buf = new byte[bytes.Length + 1];
        bytes.CopyTo(buf);
        // buf[bytes.Length] is already 0 (zero-initialised)

        for (int i = 1; i <= bytes.Length; i++)
        {
            bool isLeaf = i == bytes.Length;
            bool isSep  = !isLeaf && bytes[i] == (byte)'/';
            if (!isLeaf && !isSep) continue;

            // Null-terminate at i (already 0 if isLeaf)
            var saved = buf[i];
            buf[i] = 0;
            fixed (byte* p = buf)
            {
                var r = Libc.Mkdir(p, 0x1ED /* 0755 */);
                if (r != 0 && Libc.Errno() != 17 /* EEXIST */ && isLeaf)
                    throw new IOException($"mkdir failed: errno {Libc.Errno()}");
            }
            buf[i] = saved;
        }
    }

    // ── symlinks ─────────────────────────────────────────────────────────────

    public static void CreateSymbolicLink(FastString link, FastString target)
    {
        // libc symlink(target, linkpath)
        var targetBuf = NullTerminate(target);
        var linkBuf = NullTerminate(link);
        fixed (byte* t = targetBuf)
        fixed (byte* l = linkBuf)
        {
            if (Libc.Symlink(t, l) != 0)
                throw new IOException($"symlink failed: errno {Libc.Errno()}");
        }
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

    public static void DeleteFile(FastString path)
    {
        var buf = NullTerminate(path);
        int ret;
        fixed (byte* p = buf) ret = Libc.Unlink(p);
        if (ret != 0) throw new IOException($"unlink failed: errno {Libc.Errno()}");
    }

    // ── path manipulation ────────────────────────────────────────────────────

    public static byte[] GetCurrentDirectory()
    {
        var buf = new byte[4096];
        fixed (byte* p = buf)
        {
            var ptr = Libc.Getcwd(p, (nuint)buf.Length);
            if (ptr == IntPtr.Zero) throw new IOException("getcwd failed");
            return SliceToNull(buf);
        }
    }

    public static byte[] GetFullPath(FastString path)
    {
        if (path.IsEmpty) return GetCurrentDirectory();
        if (path.Bytes[0] == (byte)'/') return NormalizePath(path.Bytes);
        var cwd = GetCurrentDirectory();
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

        // Find the length of the common path prefix (at segment boundaries)
        int commonLen = 0;
        int minLen = Math.Min(fb.Length, tb.Length);
        for (int i = 0; i < minLen; i++)
        {
            if (fb[i] != tb[i]) break;
            if (fb[i] == (byte)'/') commonLen = i; // update after each slash
        }
        // If one is a prefix of the other, update commonLen
        if (minLen > 0 && minLen <= fb.Length && minLen <= tb.Length)
        {
            // If we matched all of the shorter one and the next char in longer is '/'
            if (fb.Length == minLen && tb.Length > minLen && tb[minLen] == (byte)'/') commonLen = minLen;
            else if (tb.Length == minLen && fb.Length > minLen && fb[minLen] == (byte)'/') commonLen = minLen;
        }

        // Tail of 'from' after the common prefix (segments we need to go up)
        // If commonLen == 0 and paths share no prefix, fromTail = all of fb
        ReadOnlySpan<byte> fromTail = commonLen == 0 ? fb
            : (commonLen < fb.Length ? fb[(commonLen + 1)..] : ReadOnlySpan<byte>.Empty);
        int hops = 0;
        if (!fromTail.IsEmpty)
        {
            hops = 1;
            foreach (var b in fromTail)
                if (b == (byte)'/') hops++;
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
        foreach (var dir in CollectEntries(path.Bytes.ToArray(), Libc.DT_DIR))
            all.AddRange(EnumerateFiles(dir, recursive: true));
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

                    // macOS dirent (_DARWIN_USE_64_BIT_INODE):
                    // d_ino(8) d_seekoff(8) d_reclen(2) d_namlen(2) d_type(1) d_name[...]
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
        return buf; // buf[Length] is 0 (array is zero-initialised)
    }

    private static byte[] SliceToNull(byte[] buf)
    {
        int len = 0;
        while (len < buf.Length && buf[len] != 0) len++;
        return buf[..len];
    }
}
