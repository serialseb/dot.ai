using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Dotai.Native;

public unsafe struct NativeString
{
    private byte* _ptr;
    private int _len;

    private NativeString(byte* ptr, int len) { _ptr = ptr; _len = len; }

    public int Length => _len;
    public bool IsEmpty => _len == 0;
    public NativeStringView AsView() => new(new ReadOnlySpan<byte>(_ptr, _len));

    public static NativeString From(NativeStringView src)
    {
        if (src.Length == 0) return new NativeString(null, 0);
        byte* p = (byte*)NativeMemory.Alloc((nuint)src.Length);
        src.Bytes.CopyTo(new Span<byte>(p, src.Length));
        return new NativeString(p, src.Length);
    }

    public static NativeString FromNullTerminated(byte* src)
    {
        if (src == null) return new NativeString(null, 0);
        int len = 0; while (src[len] != 0) len++;
        byte* p = (byte*)NativeMemory.Alloc((nuint)len);
        new ReadOnlySpan<byte>(src, len).CopyTo(new Span<byte>(p, len));
        return new NativeString(p, len);
    }

    public static NativeString From(int capacity, delegate* managed<byte*, int, int> fill)
    {
        byte* p = (byte*)NativeMemory.Alloc((nuint)capacity);
        int actual = fill(p, capacity);
        if (actual < 0) { NativeMemory.Free(p); Fatal.Die("NativeString.From fill returned negative"u8); return default; }
        return new NativeString(p, actual);
    }

    public void Dispose()
    {
        if (_ptr != null) { NativeMemory.Free(_ptr); _ptr = null; _len = 0; }
    }

    // Internal: used by NativeBuffer.Freeze() to wrap owned memory without copying.
    internal static NativeString Wrap(byte* ptr, int len) => new(ptr, len);
}
