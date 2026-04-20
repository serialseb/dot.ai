using System.Text.Json;
using Dotai.Native;
using Dotai.Ui;

namespace Dotai.Services;

public static class ConfigStore
{
    private static readonly JsonReaderOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static bool TryLoad(NativeStringView path, out NativeList<NativeString> config)
    {
        if (!Fs.Exists(path))
        {
            config = new NativeList<NativeString>(4);
            return true;
        }

        if (!Fs.TryReadAllBytes(path, out var bytes))
        {
            config = new NativeList<NativeString>(0);
            return false;
        }

        var result = new NativeList<NativeString>(4);
        var reader = new Utf8JsonReader(bytes.AsView().Bytes, ReadOptions);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            EmitMalformedError(path);
            bytes.Dispose();
            config = new NativeList<NativeString>(0);
            return false;
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                EmitMalformedError(path);
                bytes.Dispose();
                for (int i = 0; i < result.Length; i++) result[i].Dispose();
                result.Dispose();
                config = new NativeList<NativeString>(0);
                return false;
            }

            NativeString keyNs;
            if (reader.HasValueSequence)
            {
                var seq = reader.ValueSequence;
                var keyBuf = new NativeBuffer((int)seq.Length);
                var seqEnum = seq.GetEnumerator();
                while (seqEnum.MoveNext())
                    keyBuf.Append(new NativeStringView(seqEnum.Current.Span));
                keyNs = keyBuf.Freeze();
            }
            else
            {
                keyNs = NativeString.From(new NativeStringView(reader.ValueSpan));
            }

            if (!reader.Read())
            {
                EmitMalformedError(path);
                keyNs.Dispose();
                bytes.Dispose();
                for (int i = 0; i < result.Length; i++) result[i].Dispose();
                result.Dispose();
                config = new NativeList<NativeString>(0);
                return false;
            }

            SkipValue(ref reader);
            result.Add(keyNs);
        }

        bytes.Dispose();
        config = result;
        return true;
    }

    public static void AddRepo(ref NativeList<NativeString> config, NativeStringView uri)
    {
        for (int i = 0; i < config.Length; i++)
            if (config[i].AsView() == uri) return;
        config.Add(NativeString.From(uri));
    }

    public static void Save(NativeStringView path, NativeList<NativeString> config)
    {
        var parent = Fs.GetDirectoryName(path);
        if (!parent.IsEmpty) Fs.TryCreateDirectory(parent.AsView());
        parent.Dispose();

        using var stream = new MemoryStream();
        var writerOptions = new JsonWriterOptions { Indented = true };
        using (var writer = new Utf8JsonWriter(stream, writerOptions))
        {
            writer.WriteStartObject();
            for (int i = 0; i < config.Length; i++)
            {
                writer.WritePropertyName(config[i].AsView().Bytes);
                writer.WriteStartObject();
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        Fs.TryWriteAllBytes(path, new NativeStringView(stream.ToArray()));
    }

    private static void EmitMalformedError(NativeStringView path)
    {
        var buf = new NativeBuffer(path.Length + 64);
        buf.Append("config at "u8);
        buf.Append(path);
        buf.Append(" is malformed. Fix it, or rerun with --force."u8);
        ConsoleOut.Error(buf.AsView());
        buf.Dispose();
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
