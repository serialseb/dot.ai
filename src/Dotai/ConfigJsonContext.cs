using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dotai;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
internal partial class ConfigJsonContext : JsonSerializerContext { }
