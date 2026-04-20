namespace Dotai.Native;

#pragma warning disable CS0660, CS0661
public readonly ref struct NativeStringView
{
    public readonly ReadOnlySpan<byte> Bytes;
    public NativeStringView(ReadOnlySpan<byte> b) { Bytes = b; }
    public int Length => Bytes.Length;
    public bool IsEmpty => Bytes.IsEmpty;
    public byte this[int i] => Bytes[i];
    public NativeStringView Slice(int start) => new(Bytes[start..]);
    public NativeStringView Slice(int start, int length) => new(Bytes.Slice(start, length));
    public bool StartsWith(NativeStringView p) => Bytes.StartsWith(p.Bytes);
    public bool EndsWith(NativeStringView s) => Bytes.EndsWith(s.Bytes);
    public int IndexOf(byte b) => Bytes.IndexOf(b);
    public int LastIndexOf(byte b) => Bytes.LastIndexOf(b);
    public NativeStringView Trim()
    {
        var s = Bytes;
        int i = 0, j = s.Length;
        while (i < j && IsWs(s[i])) i++;
        while (j > i && IsWs(s[j - 1])) j--;
        return new NativeStringView(s[i..j]);
    }
    public bool IsBlank()
    {
        for (int i = 0; i < Bytes.Length; i++)
            if (!IsWs(Bytes[i])) return false;
        return true;
    }
    private static bool IsWs(byte b) => b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n';
    public static implicit operator NativeStringView(ReadOnlySpan<byte> s) => new(s);
    public static implicit operator NativeStringView(byte[] s) => new(s);
    public static implicit operator ReadOnlySpan<byte>(NativeStringView v) => v.Bytes;
    public static bool operator ==(NativeStringView a, NativeStringView b) => a.Bytes.SequenceEqual(b.Bytes);
    public static bool operator !=(NativeStringView a, NativeStringView b) => !a.Bytes.SequenceEqual(b.Bytes);
    public static bool operator ==(NativeStringView a, ReadOnlySpan<byte> b) => a.Bytes.SequenceEqual(b);
    public static bool operator !=(NativeStringView a, ReadOnlySpan<byte> b) => !a.Bytes.SequenceEqual(b);

    public int IndexOf(NativeStringView needle)
    {
        for (int i = 0; i <= Bytes.Length - needle.Length; i++)
            if (Bytes.Slice(i, needle.Length).SequenceEqual(needle.Bytes)) return i;
        return -1;
    }

    public bool Contains(byte b) => IndexOf(b) >= 0;
    public bool Contains(NativeStringView needle) => IndexOf(needle) >= 0;

    public int LastIndexOf(NativeStringView needle)
    {
        for (int i = Bytes.Length - needle.Length; i >= 0; i--)
            if (Bytes.Slice(i, needle.Length).SequenceEqual(needle.Bytes)) return i;
        return -1;
    }

    public NativeStringView Take(int length) => new(Bytes[..length]);
}
#pragma warning restore CS0660, CS0661
