namespace Dotai.Text;

#pragma warning disable CS0660, CS0661
public readonly ref struct FastString
{
    public readonly ReadOnlySpan<byte> Bytes;

    public FastString(ReadOnlySpan<byte> bytes) { Bytes = bytes; }

    public int Length => Bytes.Length;
    public bool IsEmpty => Bytes.IsEmpty;

    public static implicit operator FastString(ReadOnlySpan<byte> s) => new(s);
    public static implicit operator FastString(byte[] s) => new(s);
    public static implicit operator ReadOnlySpan<byte>(FastString s) => s.Bytes;

    public bool StartsWith(FastString prefix) => Bytes.StartsWith(prefix.Bytes);
    public bool EndsWith(FastString suffix) => Bytes.EndsWith(suffix.Bytes);
    public bool Equals(FastString other) => Bytes.SequenceEqual(other.Bytes);

    public static bool operator ==(FastString left, FastString right)
        => left.Bytes.SequenceEqual(right.Bytes);

    public static bool operator !=(FastString left, FastString right)
        => !left.Bytes.SequenceEqual(right.Bytes);

    public static bool operator ==(FastString left, ReadOnlySpan<byte> right)
        => left.Bytes.SequenceEqual(right);

    public static bool operator !=(FastString left, ReadOnlySpan<byte> right)
        => !left.Bytes.SequenceEqual(right);

    public int IndexOf(byte b) => Bytes.IndexOf(b);
    public int LastIndexOf(byte b) => Bytes.LastIndexOf(b);
    public FastString Slice(int start) => new(Bytes[start..]);
    public FastString Slice(int start, int length) => new(Bytes.Slice(start, length));
    public FastString Trim() => new(TrimSpan(Bytes));

    private static ReadOnlySpan<byte> TrimSpan(ReadOnlySpan<byte> s)
    {
        int i = 0, j = s.Length;
        while (i < j && IsWs(s[i])) i++;
        while (j > i && IsWs(s[j - 1])) j--;
        return s[i..j];
    }

    private static bool IsWs(byte b) => b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n';

    public static unsafe FastString FromNullTerminated(byte* ptr)
    {
        if (ptr == null) return default;
        int len = 0;
        while (ptr[len] != 0) len++;
        return new FastString(new ReadOnlySpan<byte>(ptr, len));
    }

    public static unsafe byte[] CloneNullTerminated(byte* ptr)
    {
        if (ptr == null) return Array.Empty<byte>();
        int len = 0;
        while (ptr[len] != 0) len++;
        var result = new byte[len];
        new ReadOnlySpan<byte>(ptr, len).CopyTo(result);
        return result;
    }
}
#pragma warning restore CS0660, CS0661
