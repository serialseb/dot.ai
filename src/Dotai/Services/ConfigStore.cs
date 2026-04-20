using System.Text.Json;
using Dotai.Text;
using Dotai.Ui;

namespace Dotai.Services;

public static class ConfigStore
{
    private static readonly JsonReaderOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // Byte-native API used by production code.
    public static bool TryLoad(FastString path, out List<byte[]> config)
    {
        if (!Fs.Exists(path))
        {
            config = new List<byte[]>();
            return true;
        }

        var bytes = Fs.ReadAllBytes(path);
        var reader = new Utf8JsonReader(bytes, ReadOptions);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            EmitMalformedError(path);
            config = new List<byte[]>();
            return false;
        }

        var result = new List<byte[]>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                EmitMalformedError(path);
                config = new List<byte[]>();
                return false;
            }

            byte[] keyBytes;
            if (reader.HasValueSequence)
            {
                var seq = reader.ValueSequence;
                keyBytes = new byte[(int)seq.Length];
                int pos = 0;
                foreach (var segment in seq)
                {
                    segment.Span.CopyTo(keyBytes.AsSpan(pos));
                    pos += segment.Length;
                }
            }
            else
            {
                keyBytes = reader.ValueSpan.ToArray();
            }

            if (!reader.Read())
            {
                EmitMalformedError(path);
                config = new List<byte[]>();
                return false;
            }

            SkipValue(ref reader);
            result.Add(keyBytes);
        }

        config = result;
        return true;
    }

    public static void AddRepo(List<byte[]> config, FastString uri)
    {
        if (ContainsBytes(config, uri)) return;
        config.Add(uri.Bytes.ToArray());
    }

    // Byte-native API used by production code.
    public static void Save(FastString path, List<byte[]> config)
    {
        var parent = Fs.GetDirectoryName(path);
        if (parent.Length > 0) Fs.CreateDirectory(parent);

        using var stream = new MemoryStream();
        var writerOptions = new JsonWriterOptions { Indented = true };
        using (var writer = new Utf8JsonWriter(stream, writerOptions))
        {
            writer.WriteStartObject();
            foreach (var urlBytes in config)
            {
                writer.WritePropertyName(urlBytes.AsSpan());
                writer.WriteStartObject();
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        Fs.WriteAllBytes(path, stream.ToArray());
    }

    private static void EmitMalformedError(FastString path)
    {
        var buf = new ByteBuffer(path.Bytes.Length + 64);
        buf.Append("config at "u8);
        buf.Append(path.Bytes);
        buf.Append(" is malformed. Fix it, or rerun with --force."u8);
        ConsoleOut.Error(buf.Span);
    }

    private static bool ContainsBytes(List<byte[]> list, FastString candidate)
    {
        foreach (var item in list)
            if (candidate == new FastString(item)) return true;
        return false;
    }

    private static void SkipValue(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                var depth = 1;
                while (depth > 0 && reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray) depth++;
                    else if (reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray) depth--;
                }
                break;
        }
    }
}
