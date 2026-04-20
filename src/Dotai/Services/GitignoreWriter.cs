namespace Dotai.Services;

public static class GitignoreWriter
{
    public static void EnsureLine(string path, string line)
    {
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

        if (!File.Exists(path))
        {
            File.WriteAllText(path, line + Environment.NewLine);
            return;
        }

        var lines = File.ReadAllLines(path);
        foreach (var existing in lines)
        {
            if (existing.Trim() == line) return;
        }

        var text = File.ReadAllText(path);
        if (text.Length > 0 && !text.EndsWith('\n')) text += Environment.NewLine;
        text += line + Environment.NewLine;
        File.WriteAllText(path, text);
    }
}
