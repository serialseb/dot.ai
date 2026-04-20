namespace Dotai.Services;

internal static class ByteOps
{
    public static bool IsBlank(ReadOnlySpan<byte> s)
    {
        foreach (var b in s)
            if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\r' && b != (byte)'\n')
                return false;
        return true;
    }

    public static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> s)
    {
        int i = 0, j = s.Length;
        while (i < j && IsWs(s[i])) i++;
        while (j > i && IsWs(s[j - 1])) j--;
        return s[i..j];
    }

    private static bool IsWs(byte b) =>
        b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n';

    public static ReadOnlySpan<byte> GetDefaultBranchFromSymbolicRef(ReadOnlySpan<byte> line)
    {
        // input like "refs/remotes/origin/main\n"
        line = Trim(line);
        for (int i = line.Length - 1; i >= 0; i--)
            if (line[i] == (byte)'/') return line[(i + 1)..];
        return line;
    }

    public static byte[] Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var r = new byte[a.Length + b.Length];
        a.CopyTo(r);
        b.CopyTo(r.AsSpan(a.Length));
        return r;
    }
}
