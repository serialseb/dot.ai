using System.Text;
using System.Text.Json;

namespace Dotai.Services;

public static class ConfigStore
{
    private static readonly JsonReaderOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static List<string> Load(string path)
    {
        if (!File.Exists(path)) return new List<string>();

        var bytes = File.ReadAllBytes(path);
        var reader = new Utf8JsonReader(bytes, ReadOptions);

        if (!reader.Read())
            throw new InvalidDataException($"Config file '{path}' is empty.");
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new InvalidDataException($"Config file '{path}' must contain a JSON object at the root.");

        var result = new List<string>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new InvalidDataException($"Unexpected token {reader.TokenType} in '{path}'.");

            var key = reader.GetString()!;
            if (!reader.Read())
                throw new InvalidDataException($"Truncated value for key '{key}' in '{path}'.");

            SkipValue(ref reader); // accept any value, discard — values are reserved for the future
            result.Add(key);
        }

        return result;
    }

    public static void AddRepo(List<string> config, string uri)
    {
        if (!config.Contains(uri)) config.Add(uri);
    }

    public static void Save(string path, List<string> config)
    {
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

        using var stream = new MemoryStream();
        var writerOptions = new JsonWriterOptions { Indented = true };
        using (var writer = new Utf8JsonWriter(stream, writerOptions))
        {
            writer.WriteStartObject();
            foreach (var url in config)
            {
                writer.WritePropertyName(url);
                writer.WriteStartObject();
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        File.WriteAllBytes(path, stream.ToArray());
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
