using System.Text;
using System.Text.Json;
using Dotai.Text;

namespace Dotai.Services;

public static class ConfigStore
{
    private static readonly JsonReaderOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static List<byte[]> Load(string path)
    {
        if (!File.Exists(path)) return new List<byte[]>();

        var bytes = File.ReadAllBytes(path);
        var reader = new Utf8JsonReader(bytes, ReadOptions);

        if (!reader.Read())
            throw new InvalidDataException($"Config file '{path}' is empty.");
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new InvalidDataException($"Config file '{path}' must contain a JSON object at the root.");

        var result = new List<byte[]>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new InvalidDataException($"Unexpected token {reader.TokenType} in '{path}'.");

            // Read the raw UTF-8 property name bytes without decoding to string.
            // HasValueSequence is rare (large property names spanning buffer segments).
            // For simplicity fall back to GetString only in that path.
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
                throw new InvalidDataException($"Truncated value for a key in '{path}'.");

            SkipValue(ref reader); // accept any value, discard — values are reserved for the future
            result.Add(keyBytes);
        }

        return result;
    }

    public static void AddRepo(List<byte[]> config, FastString uri)
    {
        if (ContainsBytes(config, uri)) return;
        config.Add(uri.Bytes.ToArray());
    }

    public static void Save(string path, List<byte[]> config)
    {
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

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
        File.WriteAllBytes(path, stream.ToArray());
    }

    private static bool ContainsBytes(List<byte[]> list, FastString candidate)
    {
        foreach (var item in list)
            if (candidate.Equals(new FastString(item))) return true;
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
            // scalar — already positioned on the value token, nothing to skip
        }
    }
}
