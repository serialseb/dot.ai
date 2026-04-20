using Dotai.Native;
using Dotai.Ui;

namespace Dotai.Services;

public static class ConfigStore
{
    public static bool TryLoad(NativeStringView path, out NativeList<RepoConfig> config)
    {
        if (!Fs.TryReadAllBytes(path, out var fileBytes))
        {
            config = new NativeList<RepoConfig>(4);
            return true; // missing → empty
        }

        if (!TomlConfig.TryParse(fileBytes.AsView(), out config))
        {
            fileBytes.Dispose();
            return false;
        }

        fileBytes.Dispose();
        return true;
    }

    public static void Save(NativeStringView path, NativeListView<RepoConfig> config)
    {
        var parent = Fs.GetDirectoryName(path);
        if (!parent.IsEmpty) Fs.TryCreateDirectory(parent.AsView());
        parent.Dispose();

        var buf = new NativeBuffer(256);
        TomlConfig.Write(config, ref buf);
        if (!Fs.TryWriteAllBytes(path, buf.AsView()))
            Fatal.Die("cannot write config"u8);
        buf.Dispose();
    }

    public static bool Contains(NativeListView<RepoConfig> config, NativeStringView name)
        => TomlConfig.Contains(config, name);

    public static void AddRepo(ref NativeList<RepoConfig> config, NativeStringView name, NativeStringView mode)
    {
        for (int i = 0; i < config.Length; i++)
            if (config[i].Name.AsView() == name) return;
        var entry = new RepoConfig
        {
            Name = NativeString.From(name),
            Mode = NativeString.From(mode)
        };
        config.Add(entry);
    }
}
