using System.Runtime.InteropServices;

namespace Dotai.Native;

public unsafe struct NativeBuffer
{
    private byte* _ptr;
    private int _len;
    private int _cap;

    public NativeBuffer(int initialCapacity)
    {
        _cap = initialCapacity < 16 ? 16 : initialCapacity;
        _ptr = (byte*)NativeMemory.Alloc((nuint)_cap);
        _len = 0;
    }

    public int Length => _len;
    public NativeStringView AsView() => new(new ReadOnlySpan<byte>(_ptr, _len));

    public void Append(NativeStringView src)
    {
        EnsureCapacity(_len + src.Length);
        src.Bytes.CopyTo(new Span<byte>(_ptr + _len, src.Length));
        _len += src.Length;
    }

    public void AppendByte(byte b)
    {
        EnsureCapacity(_len + 1);
        _ptr[_len++] = b;
    }

    public void AppendNewline() => AppendByte((byte)'\n');

    public void AppendInt(int value)
    {
        if (value == 0) { AppendByte((byte)'0'); return; }
        bool neg = value < 0;
        if (neg) { AppendByte((byte)'-'); value = -value; }
        Span<byte> digits = stackalloc byte[11];
        int i = digits.Length;
        while (value > 0) { digits[--i] = (byte)('0' + value % 10); value /= 10; }
        EnsureCapacity(_len + (digits.Length - i));
        digits[i..].CopyTo(new Span<byte>(_ptr + _len, digits.Length - i));
        _len += digits.Length - i;
    }

    public NativeString Freeze()
    {
        var result = NativeString.Wrap(_ptr, _len);
        _ptr = null;
        _len = 0;
        _cap = 0;
        return result;
    }

    private void EnsureCapacity(int need)
    {
        if (need <= _cap) return;
        int newCap = _cap * 2;
        while (newCap < need) newCap *= 2;
        byte* np = (byte*)NativeMemory.Alloc((nuint)newCap);
        if (_len > 0) new ReadOnlySpan<byte>(_ptr, _len).CopyTo(new Span<byte>(np, _len));
        NativeMemory.Free(_ptr);
        _ptr = np;
        _cap = newCap;
    }

    public void Dispose()
    {
        if (_ptr != null) { NativeMemory.Free(_ptr); _ptr = null; _len = 0; _cap = 0; }
    }
}
