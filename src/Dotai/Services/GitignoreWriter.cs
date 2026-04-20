using System.Text;
using Dotai.Text;

namespace Dotai.Services;

public static class GitignoreWriter
{
    // Byte-native API used by production code.
    public static void EnsureLine(FastString path, FastString line)
    {
        var parent = Fs.GetDirectoryName(path);
        if (parent.Length > 0) Fs.CreateDirectory(parent);

        if (!Fs.Exists(path))
        {
            // Write line + '\n'
            var buf = new byte[line.Length + 1];
            line.Bytes.CopyTo(buf);
            buf[line.Length] = (byte)'\n';
            Fs.WriteAllBytes(path, buf);
            return;
        }

        var text = Fs.ReadAllBytes(path);
        // Check each line for a match
        var textSpan = text.AsSpan();
        int start = 0;
        while (start <= textSpan.Length)
        {
            int end = textSpan[start..].IndexOf((byte)'\n');
            var lineSpan = end < 0 ? textSpan[start..] : textSpan[start..(start + end)];
            // Trim CR
            if (!lineSpan.IsEmpty && lineSpan[^1] == (byte)'\r') lineSpan = lineSpan[..^1];
            if (lineSpan.SequenceEqual(line.Bytes)) return; // already present
            if (end < 0) break;
            start += end + 1;
        }

        // Append: ensure trailing newline then add line + '\n'
        bool hasTrailingNewline = text.Length > 0 && text[^1] == (byte)'\n';
        int extra = (hasTrailingNewline ? 0 : 1) + line.Length + 1;
        var appended = new byte[text.Length + extra];
        text.CopyTo(appended, 0);
        int pos = text.Length;
        if (!hasTrailingNewline) appended[pos++] = (byte)'\n';
        line.Bytes.CopyTo(appended.AsSpan(pos));
        pos += line.Length;
        appended[pos] = (byte)'\n';
        Fs.WriteAllBytes(path, appended);
    }

    // String shim retained for tests (tests stay in .NET-land).
    public static void EnsureLine(string path, string line)
    {
        FastString pathFs = Encoding.UTF8.GetBytes(path);
        FastString lineFs = Encoding.UTF8.GetBytes(line);
        EnsureLine(pathFs, lineFs);
    }
}
