namespace Dotai.Text;

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
}
