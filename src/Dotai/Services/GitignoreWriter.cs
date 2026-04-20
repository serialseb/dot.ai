using Dotai.Native;

namespace Dotai.Services;

public static class GitignoreWriter
{
    public static void EnsureLine(NativeStringView path, NativeStringView line)
    {
        var parent = Fs.GetDirectoryName(path);
        if (!parent.IsEmpty) Fs.TryCreateDirectory(parent.AsView());
        parent.Dispose();

        if (!Fs.Exists(path))
        {
            var newBuf = new NativeBuffer(line.Length + 1);
            newBuf.Append(line);
            newBuf.AppendByte((byte)'\n');
            var ns = newBuf.Freeze();
            Fs.TryWriteAllBytes(path, ns.AsView());
            ns.Dispose();
            return;
        }

        if (!Fs.TryReadAllBytes(path, out var text)) return;

        var textView = text.AsView();
        var textBytes = textView.Bytes;
        int start = 0;
        while (start <= textBytes.Length)
        {
            int end = textBytes[start..].IndexOf((byte)'\n');
            var lineSpan = end < 0 ? textBytes[start..] : textBytes[start..(start + end)];
            if (!lineSpan.IsEmpty && lineSpan[^1] == (byte)'\r') lineSpan = lineSpan[..^1];
            if (lineSpan.SequenceEqual(line.Bytes)) { text.Dispose(); return; }
            if (end < 0) break;
            start += end + 1;
        }

        bool hasTrailingNewline = textBytes.Length > 0 && textBytes[^1] == (byte)'\n';
        var appendBuf = new NativeBuffer(textBytes.Length + (hasTrailingNewline ? 0 : 1) + line.Length + 1);
        appendBuf.Append(textView);
        if (!hasTrailingNewline) appendBuf.AppendByte((byte)'\n');
        appendBuf.Append(line);
        appendBuf.AppendByte((byte)'\n');
        var appended = appendBuf.Freeze();
        text.Dispose();
        Fs.TryWriteAllBytes(path, appended.AsView());
        appended.Dispose();
    }
}
