using System.Runtime.InteropServices;
using Dotai.Native;
using Dotai.Ui;

namespace Dotai.Services;

// libc-backed filesystem helpers. All paths are null-terminated UTF-8 byte buffers.
// macOS only (see Libc.cs for the portability note).
// All mutating operations return bool; on failure they emit ConsoleOut.Error and return false.
public static unsafe class Fs
{
    // ── existence / type ────────────────────────────────────────────────────

    public static bool Exists(NativeStringView path)
    {
        byte* buf = stackalloc byte[MaxStack];
        byte* p = NullTerm(path, buf);
        int ret = Libc.Access(p, Libc.F_OK);
        FreeHeap(path, p, buf);
        return ret == 0;
    }

    public static bool IsDirectory(NativeStringView path)
    {
        byte* statbuf = stackalloc byte[Libc.StatBufSize];
        byte* buf = stackalloc byte[MaxStack];
        byte* p = NullTerm(path, buf);
        int ret = Libc.LStat(p, statbuf);
        FreeHeap(path, p, buf);
        if (ret != 0) return false;
        var mode = Libc.ReadMode(new ReadOnlySpan<byte>(statbuf, Libc.StatBufSize));
        return (mode & Libc.S_IFMT) == Libc.S_IFDIR;
    }

    public static bool IsSymlink(NativeStringView path)
    {
        byte* statbuf = stackalloc byte[Libc.StatBufSize];
        byte* buf = stackalloc byte[MaxStack];
        byte* p = NullTerm(path, buf);
        int ret = Libc.LStat(p, statbuf);
        FreeHeap(path, p, buf);
        if (ret != 0) return false;
        var mode = Libc.ReadMode(new ReadOnlySpan<byte>(statbuf, Libc.StatBufSize));
        return (mode & Libc.S_IFMT) == Libc.S_IFLNK;
    }

    // ── read / write ────────────────────────────────────────────────────────

    public static bool TryReadAllBytes(NativeStringView path, out NativeString bytes)
    {
        byte* buf = stackalloc byte[MaxStack];
        byte* p = NullTerm(path, buf);
        int fd = Libc.Open(p, Libc.O_RDONLY, 0);
        FreeHeap(path, p, buf);
        if (fd < 0)
        {
            EmitError("open failed: "u8, path);
            bytes = default;
            return false;
        }
        try
        {
            byte* statbuf = stackalloc byte[Libc.StatBufSize];
            if (Libc.FStat(fd, statbuf) != 0)
            {
                EmitError("fstat failed: "u8, path);
                bytes = default;
                return false;
            }
            var size = (int)BitConverter.ToInt64(new ReadOnlySpan<byte>(statbuf, Libc.StatBufSize).Slice(96, 8));
            if (size == 0) { bytes = default; return true; }
            byte* data = (byte*)NativeMemory.Alloc((nuint)size);
            long total = 0;
            while (total < size)
            {
                var n = Libc.Read(fd, data + total, (nuint)(size - total));
                if (n <= 0) break;
                total += n;
            }
            bytes = NativeString.Wrap(data, (int)total);
            return true;
        }
        finally { Libc.Close(fd); }
    }

    public static bool TryWriteAllBytes(NativeStringView path, NativeStringView data)
    {
        byte* buf = stackalloc byte[MaxStack];
        byte* p = NullTerm(path, buf);
        int fd = Libc.Creat(p, 0x1A4 /* 0644 */);
        FreeHeap(path, p, buf);
        if (fd < 0)
        {
            EmitError("creat failed: "u8, path);
            return false;
        }
        try
        {
            fixed (byte* dp = data.Bytes)
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

    public static bool TryCreateDirectory(NativeStringView path)
    {
        var bytes = path.Bytes;
        // Need a mutable null-terminated copy for iterative mkdir.
        // Use heap since we need to mutate it.
        byte* pathBuf = (byte*)NativeMemory.Alloc((nuint)(bytes.Length + 1));
        bytes.CopyTo(new Span<byte>(pathBuf, bytes.Length));
        pathBuf[bytes.Length] = 0;
        try
        {
            for (int i = 1; i <= bytes.Length; i++)
            {
                bool isLeaf = i == bytes.Length;
                bool isSep = !isLeaf && pathBuf[i] == (byte)'/';
                if (!isLeaf && !isSep) continue;

                var saved = pathBuf[i];
                pathBuf[i] = 0;
                var r = Libc.Mkdir(pathBuf, 0x1ED /* 0755 */);
                if (r != 0 && Libc.Errno() != 17 /* EEXIST */ && isLeaf)
                {
                    EmitError("mkdir failed: "u8, path);
                    pathBuf[i] = saved;
                    return false;
                }
                pathBuf[i] = saved;
            }
            return true;
        }
        finally { NativeMemory.Free(pathBuf); }
    }

    // ── symlinks ─────────────────────────────────────────────────────────────

    public static bool TryCreateSymbolicLink(NativeStringView link, NativeStringView target)
    {
        byte* tbuf = stackalloc byte[MaxStack];
        byte* lbuf = stackalloc byte[MaxStack];
        byte* tp = NullTerm(target, tbuf);
        byte* lp = NullTerm(link, lbuf);
        int ret = Libc.Symlink(tp, lp);
        FreeHeap(target, tp, tbuf);
        FreeHeap(link, lp, lbuf);
        if (ret != 0)
        {
            EmitError("symlink failed: "u8, link);
            return false;
        }
        return true;
    }

    public static bool TryReadSymbolicLinkTarget(NativeStringView link, out NativeString target)
    {
        if (!IsSymlink(link)) { target = default; return false; }
        byte* lbuf = stackalloc byte[MaxStack];
        byte* lp = NullTerm(link, lbuf);
        const int SmallBuf = 256;
        byte* outBuf = stackalloc byte[SmallBuf];
        long len = Libc.ReadLink(lp, outBuf, SmallBuf);
        if (len < 0) { FreeHeap(link, lp, lbuf); target = default; return false; }
        if (len < SmallBuf)
        {
            FreeHeap(link, lp, lbuf);
            target = NativeString.From(new NativeStringView(new ReadOnlySpan<byte>(outBuf, (int)len)));
            return true;
        }
        // Retry with 4096
        const int LargeBuf = 4096;
        byte* largeBuf = (byte*)NativeMemory.Alloc(LargeBuf);
        try
        {
            len = Libc.ReadLink(lp, largeBuf, LargeBuf);
            FreeHeap(link, lp, lbuf);
            if (len < 0) { target = default; return false; }
            target = NativeString.From(new NativeStringView(new ReadOnlySpan<byte>(largeBuf, (int)len)));
            return true;
        }
        finally { NativeMemory.Free(largeBuf); }
    }

    // ── delete ───────────────────────────────────────────────────────────────

    public static bool TryDeleteFile(NativeStringView path)
    {
        byte* buf = stackalloc byte[MaxStack];
        byte* p = NullTerm(path, buf);
        int ret = Libc.Unlink(p);
        FreeHeap(path, p, buf);
        if (ret != 0)
        {
            EmitError("unlink failed: "u8, path);
            return false;
        }
        return true;
    }

    // Resolves all symlinks via libc realpath(3). Fails if any path component
    // does not exist. Buffer is fixed at PATH_MAX (1024) — adequate for macOS.
    public static bool TryResolveRealpath(NativeStringView path, out NativeString resolved)
    {
        byte* buf = stackalloc byte[MaxStack];
        byte* p = NullTerm(path, buf);
        const int PathMax = 1024;
        byte* resBuf = stackalloc byte[PathMax];
        byte* r = Libc.Realpath(p, resBuf);
        FreeHeap(path, p, buf);
        if (r == null) { resolved = default; return false; }
        int len = 0;
        while (len < PathMax && resBuf[len] != 0) len++;
        resolved = NativeString.From(new NativeStringView(new ReadOnlySpan<byte>(resBuf, len)));
        return true;
    }

    // Removes a directory and all its contents. Symlinks inside are deleted
    // (never followed). No-op when the path does not exist.
    public static bool TryDeleteDirectoryRecursive(NativeStringView path)
    {
        if (!Exists(path)) return true;
        if (IsSymlink(path)) return TryDeleteFile(path);

        var entries = EnumerateFileSystemEntries(path);
        bool ok = true;
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i].AsView();
            if (IsSymlink(e) || !IsDirectory(e)) ok &= TryDeleteFile(e);
            else ok &= TryDeleteDirectoryRecursive(e);
        }
        for (int i = 0; i < entries.Length; i++) entries[i].Dispose();
        entries.Dispose();

        byte* buf = stackalloc byte[MaxStack];
        byte* p = NullTerm(path, buf);
        int r = Libc.Rmdir(p);
        FreeHeap(path, p, buf);
        if (r != 0) { EmitError("rmdir failed: "u8, path); return false; }
        return ok;
    }

    public static bool TryRename(NativeStringView from, NativeStringView to)
    {
        byte* fbuf = stackalloc byte[MaxStack];
        byte* tbuf = stackalloc byte[MaxStack];
        byte* fp = NullTerm(from, fbuf);
        byte* tp = NullTerm(to, tbuf);
        int ret = Libc.Rename(fp, tp);
        FreeHeap(from, fp, fbuf);
        FreeHeap(to, tp, tbuf);
        if (ret != 0)
        {
            EmitError("rename failed: "u8, from);
            return false;
        }
        return true;
    }

    // ── path manipulation ────────────────────────────────────────────────────

    public static bool TryGetCurrentDirectory(out NativeString cwd)
    {
        const int BufSize = 4096;
        byte* buf = stackalloc byte[BufSize];
        var ptr = Libc.Getcwd(buf, (nuint)BufSize);
        if (ptr == IntPtr.Zero)
        {
            ConsoleOut.Error("getcwd failed"u8);
            cwd = default;
            return false;
        }
        int len = 0;
        while (len < BufSize && buf[len] != 0) len++;
        cwd = NativeString.From(new NativeStringView(new ReadOnlySpan<byte>(buf, len)));
        return true;
    }

    public static NativeString GetFullPath(NativeStringView path)
    {
        if (path.IsEmpty)
        {
            TryGetCurrentDirectory(out var cwd2);
            return cwd2;
        }
        if (path.Bytes[0] == (byte)'/') return NormalizePath(path);
        TryGetCurrentDirectory(out var cwd);
        var combined = Combine(cwd.AsView(), path);
        cwd.Dispose();
        var result = NormalizePath(combined.AsView());
        combined.Dispose();
        return result;
    }

    // Resolve . and .. segments in an absolute path without syscalls.
    private static NativeString NormalizePath(NativeStringView abs)
    {
        const int MaxSegments = 128;
        int* segStarts = stackalloc int[MaxSegments];
        int* segLens = stackalloc int[MaxSegments];
        int segCount = 0;

        var span = abs.Bytes;
        int spanOffset = 0;
        if (!span.IsEmpty && span[0] == (byte)'/') { span = span[1..]; spanOffset = 1; }
        while (!span.IsEmpty)
        {
            int sep = span.IndexOf((byte)'/');
            var seg = sep < 0 ? span : span[..sep];
            if (!seg.IsEmpty && !seg.SequenceEqual("."u8))
            {
                if (seg.SequenceEqual(".."u8))
                {
                    if (segCount > 0) segCount--;
                }
                else if (segCount < MaxSegments)
                {
                    segStarts[segCount] = spanOffset + (abs.Length - spanOffset - span.Length < 0 ? 0 : abs.Length - spanOffset - span.Length);
                    // Actually compute offset from start of abs.Bytes
                    int startInAbs = abs.Length - span.Length;
                    segStarts[segCount] = startInAbs;
                    segLens[segCount] = seg.Length;
                    segCount++;
                }
            }
            if (sep < 0) break;
            span = span[(sep + 1)..];
        }

        if (segCount == 0) return NativeString.From("/"u8);
        int totalLen = 1;
        for (int i = 0; i < segCount; i++) totalLen += (i > 0 ? 1 : 0) + segLens[i];
        var buf = new NativeBuffer(totalLen);
        buf.AppendByte((byte)'/');
        for (int i = 0; i < segCount; i++)
        {
            if (i > 0) buf.AppendByte((byte)'/');
            buf.Append(new NativeStringView(abs.Bytes.Slice(segStarts[i], segLens[i])));
        }
        return buf.Freeze();
    }

    public static NativeString GetDirectoryName(NativeStringView path)
    {
        var bytes = path.Bytes;
        while (!bytes.IsEmpty && bytes[^1] == (byte)'/') bytes = bytes[..^1];
        int slash = bytes.LastIndexOf((byte)'/');
        if (slash < 0) return NativeString.From("."u8);
        if (slash == 0) return NativeString.From("/"u8);
        return NativeString.From(new NativeStringView(bytes[..slash]));
    }

    public static NativeString GetFileName(NativeStringView path)
    {
        var bytes = path.Bytes;
        while (!bytes.IsEmpty && bytes[^1] == (byte)'/') bytes = bytes[..^1];
        int slash = bytes.LastIndexOf((byte)'/');
        return slash < 0
            ? NativeString.From(new NativeStringView(bytes))
            : NativeString.From(new NativeStringView(bytes[(slash + 1)..]));
    }

    public static NativeString Combine(NativeStringView a, NativeStringView b)
    {
        var ab = a.Bytes;
        var bb = b.Bytes;
        if (bb.IsEmpty) return NativeString.From(a);
        if (ab.IsEmpty) return NativeString.From(b);
        if (bb[0] == (byte)'/') return NativeString.From(b);
        while (!ab.IsEmpty && ab[^1] == (byte)'/') ab = ab[..^1];
        var buf = new NativeBuffer(ab.Length + 1 + bb.Length);
        buf.Append(new NativeStringView(ab));
        buf.AppendByte((byte)'/');
        buf.Append(b);
        return buf.Freeze();
    }

    public static NativeString Combine(NativeStringView a, NativeStringView b, NativeStringView c)
    {
        var ab = Combine(a, b);
        var result = Combine(ab.AsView(), c);
        ab.Dispose();
        return result;
    }

    public static NativeString GetRelativePath(NativeStringView from, NativeStringView to)
    {
        var fb = from.Bytes;
        var tb = to.Bytes;
        while (!fb.IsEmpty && fb[^1] == (byte)'/') fb = fb[..^1];
        while (!tb.IsEmpty && tb[^1] == (byte)'/') tb = tb[..^1];

        if (fb.SequenceEqual(tb)) return NativeString.From("."u8);

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
        if (totalLen == 0) return NativeString.From("."u8);
        var result = new NativeBuffer(totalLen + 1);
        for (int i = 0; i < hops; i++) { result.AppendByte((byte)'.'); result.AppendByte((byte)'.'); result.AppendByte((byte)'/'); }
        result.Append(new NativeStringView(toTail));
        var v = result.AsView().Bytes;
        while (!v.IsEmpty && v[^1] == (byte)'/') v = v[..^1];
        if (v.IsEmpty) return NativeString.From("."u8);
        return NativeString.From(new NativeStringView(v));
    }

    // ── enumeration ──────────────────────────────────────────────────────────

    public static NativeList<NativeString> EnumerateDirectories(NativeStringView path)
        => CollectEntries(path, Libc.DT_DIR);

    public static NativeList<NativeString> EnumerateFiles(NativeStringView path, bool recursive = false)
    {
        var files = CollectEntries(path, Libc.DT_REG);
        if (!recursive) return files;
        var dirs = CollectEntries(path, Libc.DT_DIR);
        for (int i = 0; i < dirs.Length; i++)
        {
            var dirView = dirs[i].AsView();
            var subFiles = EnumerateFiles(dirView, recursive: true);
            for (int j = 0; j < subFiles.Length; j++)
                files.Add(subFiles[j]);
            // subFiles elements are now owned by files — only dispose the list container
            subFiles.Dispose();
            dirs[i].Dispose();
        }
        dirs.Dispose();
        return files;
    }

    public static NativeList<NativeString> EnumerateFileSystemEntries(NativeStringView path)
        => CollectEntries(path, 0 /* all types */);

    private static NativeList<NativeString> CollectEntries(NativeStringView pathView, byte typeFilter)
    {
        var result = new NativeList<NativeString>(8);
        byte* buf = stackalloc byte[MaxStack];
        byte* p = NullTerm(pathView, buf);
        IntPtr dirp = Libc.Opendir(p);
        FreeHeap(pathView, p, buf);
        if (dirp == IntPtr.Zero) return result;
        try
        {
            while (true)
            {
                var dirent = Libc.Readdir(dirp);
                if (dirent == IntPtr.Zero) break;

                var namlen = *(ushort*)((byte*)dirent + Libc.DirentNamlenOff);
                var dtype = *((byte*)dirent + Libc.DirentTypeOff);
                var namePtr = (byte*)dirent + Libc.DirentNameOff;

                if (namlen == 1 && *namePtr == (byte)'.') continue;
                if (namlen == 2 && *namePtr == (byte)'.' && *(namePtr + 1) == (byte)'.') continue;
                if (typeFilter != 0 && dtype != typeFilter) continue;

                var nameView = new NativeStringView(new ReadOnlySpan<byte>(namePtr, namlen));
                var combined = Combine(pathView, nameView);
                result.Add(combined);
            }
        }
        finally { Libc.Closedir(dirp); }
        return result;
    }

    // ── null-termination helpers ─────────────────────────────────────────────

    // MaxStack: use stackalloc up to this many bytes. 256 covers most paths.
    private const int MaxStack = 256;

    // Returns a null-terminated pointer. If path.Length < MaxStack, uses the provided stackBuf.
    // Otherwise allocates on the heap — caller must call FreeHeap.
    private static byte* NullTerm(NativeStringView path, byte* stackBuf)
    {
        if (path.Length < MaxStack)
        {
            path.Bytes.CopyTo(new Span<byte>(stackBuf, path.Length));
            stackBuf[path.Length] = 0;
            return stackBuf;
        }
        // Heap allocation
        byte* p = (byte*)NativeMemory.Alloc((nuint)(path.Length + 1));
        path.Bytes.CopyTo(new Span<byte>(p, path.Length));
        p[path.Length] = 0;
        return p;
    }

    // Frees heap memory if NullTerm allocated it (i.e., returned != stackBuf).
    private static void FreeHeap(NativeStringView path, byte* p, byte* stackBuf)
    {
        if (p != stackBuf) NativeMemory.Free(p);
    }

    private static void EmitError(ReadOnlySpan<byte> prefix, NativeStringView path)
    {
        var b = new NativeBuffer(prefix.Length + path.Length);
        b.Append(new NativeStringView(prefix));
        b.Append(path);
        ConsoleOut.Error(b.AsView());
        b.Dispose();
    }
}
