namespace Dotai.Ui;

internal ref struct ByteBuffer
{
    private byte[] _buf;
    private int _len;

    public ByteBuffer(int initialCapacity) { _buf = new byte[initialCapacity]; _len = 0; }

    public ReadOnlySpan<byte> Span => _buf.AsSpan(0, _len);

    public void Append(ReadOnlySpan<byte> s)
    {
        EnsureCapacity(_len + s.Length);
        s.CopyTo(_buf.AsSpan(_len));
        _len += s.Length;
    }

    public void AppendByte(byte b)
    {
        EnsureCapacity(_len + 1);
        _buf[_len++] = b;
    }

    public void AppendInt(int value)
    {
        if (value == 0) { AppendByte((byte)'0'); return; }
        if (value < 0) { AppendByte((byte)'-'); value = -value; }
        // Build digits in a local array (avoids ref-struct + stackalloc span restriction).
        var digits = new byte[11];
        var i = digits.Length;
        while (value > 0) { digits[--i] = (byte)('0' + value % 10); value /= 10; }
        Append(digits.AsSpan(i));
    }

    public void AppendNewline() => AppendByte((byte)'\n');

    private void EnsureCapacity(int required)
    {
        if (required <= _buf.Length) return;
        var newSize = _buf.Length * 2;
        while (newSize < required) newSize *= 2;
        var n = new byte[newSize];
        _buf.AsSpan(0, _len).CopyTo(n);
        _buf = n;
    }
}
