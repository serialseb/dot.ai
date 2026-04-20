using Dotai.Native;
using Dotai.Ui;

namespace Dotai.Services;

public static class TomlConfig
{
    // Parses TOML content into a list of RepoConfig entries. Returns true on success.
    // On malformed input, writes red error via ConsoleOut.Error and returns false.
    public static bool TryParse(NativeStringView src, out NativeList<RepoConfig> result)
    {
        result = new NativeList<RepoConfig>(4);

        var remaining = src;
        bool hasPending = false;
        var pending = new RepoConfig();

        while (!remaining.IsEmpty)
        {
            // Find next newline
            int nl = remaining.IndexOf((byte)'\n');
            NativeStringView rawLine;
            if (nl < 0)
            {
                rawLine = remaining;
                remaining = new NativeStringView(ReadOnlySpan<byte>.Empty);
            }
            else
            {
                rawLine = remaining.Take(nl);
                remaining = remaining.Slice(nl + 1);
            }

            var line = rawLine.Trim();

            if (line.IsEmpty) continue;

            // Comment
            if (line[0] == (byte)'#') continue;

            // Section header: starts with '[' ends with ']'
            if (line[0] == (byte)'[')
            {
                if (!line.EndsWith("]"u8))
                {
                    EmitMalformedLine(line);
                    DisposePartial(ref result, ref pending, hasPending);
                    return false;
                }

                // Finalise previous pending section
                if (hasPending)
                {
                    if (pending.Mode.IsEmpty)
                        pending.Mode = NativeString.From("merge"u8);
                    result.Add(pending);
                    pending = new RepoConfig();
                }

                // Strip [ and ]
                var inner = line.Slice(1, line.Length - 2).Trim();

                // Strip surrounding quotes if present
                if (inner.Length >= 2 && inner[0] == (byte)'"' && inner[inner.Length - 1] == (byte)'"')
                    inner = inner.Slice(1, inner.Length - 2);

                if (inner.IsEmpty)
                {
                    EmitMalformedLine(line);
                    DisposePartial(ref result, ref pending, hasPending: false);
                    return false;
                }

                pending.Name = NativeString.From(inner);
                hasPending = true;
                continue;
            }

            // Key = value
            int eq = line.IndexOf((byte)'=');
            if (eq >= 0)
            {
                var key = line.Take(eq).Trim();
                var value = line.Slice(eq + 1).Trim();

                if (value.IsEmpty)
                {
                    EmitMalformedLine(line);
                    DisposePartial(ref result, ref pending, hasPending);
                    return false;
                }

                // Unquote value
                if (value.Length >= 2 && value[0] == (byte)'"' && value[value.Length - 1] == (byte)'"')
                    value = value.Slice(1, value.Length - 2);

                if (key == "mode"u8)
                {
                    if (hasPending)
                        pending.Mode = NativeString.From(value);
                    // Ignore key=value outside any section
                }
                continue;
            }

            // Unknown / malformed line
            EmitMalformedLine(line);
            DisposePartial(ref result, ref pending, hasPending);
            return false;
        }

        // EOF: finalise pending
        if (hasPending)
        {
            if (pending.Mode.IsEmpty)
                pending.Mode = NativeString.From("merge"u8);
            result.Add(pending);
        }

        return true;
    }

    // Serialises a list to a NativeBuffer (caller disposes).
    public static void Write(NativeListView<RepoConfig> config, ref NativeBuffer buf)
    {
        buf.Append("# dotai sources\n\n"u8);
        for (int i = 0; i < config.Length; i++)
        {
            buf.Append("[\""u8);
            buf.Append(config[i].Name.AsView());
            buf.Append("\"]\nmode = \""u8);
            buf.Append(config[i].Mode.AsView());
            buf.Append("\"\n\n"u8);
        }
    }

    // Checks if a name is already present.
    public static bool Contains(NativeListView<RepoConfig> config, NativeStringView name)
    {
        for (int i = 0; i < config.Length; i++)
            if (config[i].Name.AsView() == name) return true;
        return false;
    }

    private static void EmitMalformedLine(NativeStringView line)
    {
        var buf = new NativeBuffer(line.Length + 32);
        buf.Append("config: malformed line: "u8);
        buf.Append(line);
        ConsoleOut.Error(buf.AsView());
        buf.Dispose();
    }

    private static void DisposePartial(ref NativeList<RepoConfig> result, ref RepoConfig pending, bool hasPending)
    {
        if (hasPending) pending.Dispose();
        for (int i = 0; i < result.Length; i++) result[i].Dispose();
        result.Dispose();
        result = new NativeList<RepoConfig>(0);
    }
}
