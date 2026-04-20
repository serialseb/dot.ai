using System.Text.Json;

namespace Dotai.Services;

public static class ConfigStore
{
    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Dictionary<string, JsonElement> Load(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, JsonElement>();
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream, ReadOptions);
        var result = new Dictionary<string, JsonElement>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            result[prop.Name] = prop.Value.Clone();
        }
        return result;
    }

    public static void AddRepo(Dictionary<string, JsonElement> config, string uri)
    {
        if (config.ContainsKey(uri)) return;
        using var empty = JsonDocument.Parse("{}");
        config[uri] = empty.RootElement.Clone();
    }

    public static void Save(string path, Dictionary<string, JsonElement> config)
    {
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        var typeInfo = ConfigJsonContext.Default.DictionaryStringJsonElement;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(config, typeInfo);
        File.WriteAllBytes(path, bytes);
    }
}
