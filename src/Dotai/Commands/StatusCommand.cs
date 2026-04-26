using Dotai.Native;
using Dotai.Services;
using Dotai.Ui;

namespace Dotai.Commands;

// `dotai` with no arguments prints a tree listing of the configured
// repositories and the skills and files each one contributes. Kept
// read-only so running dotai from within a live project never mutates
// state by accident.
public sealed class StatusCommand
{
    private readonly NativeString _startDir;

    public StatusCommand()
    {
        Fs.TryGetCurrentDirectory(out _startDir);
    }

    public StatusCommand(NativeStringView startDir)
    {
        _startDir = NativeString.From(startDir);
    }

    public int Execute(NativeListView<NativeString> args)
    {
        if (!SharedFlags.TryParse(args, _startDir.AsView(), out var parsed)) return 1;
        var startDir = parsed.StartDir.AsView();

        if (!RepoRootResolver.TryFind(startDir, out var repoRoot))
        {
            ConsoleOut.Error("dotai requires a git repository"u8);
            parsed.Dispose();
            return 1;
        }

        var aiDir = Fs.Combine(repoRoot.AsView(), ".ai"u8);
        var configPath = Fs.Combine(aiDir.AsView(), "config.toml"u8);
        if (!ConfigStore.TryLoad(configPath.AsView(), out var config))
        {
            configPath.Dispose(); aiDir.Dispose(); repoRoot.Dispose(); parsed.Dispose();
            return 2;
        }
        configPath.Dispose();

        if (config.Length == 0)
        {
            ConsoleOut.Hint("no repositories configured. Run `dotai init <owner>/<repo>`."u8);
            config.Dispose();
            aiDir.Dispose();
            repoRoot.Dispose();
            parsed.Dispose();
            return 0;
        }

        var reposDir = Fs.Combine(aiDir.AsView(), "repositories"u8);
        aiDir.Dispose();

        for (int i = 0; i < config.Length; i++)
        {
            var spec = config[i].Name.AsView();
            EmitRepo(reposDir.AsView(), spec);
            if (i < config.Length - 1) ConsoleOut.WriteLineStdout(""u8);
        }

        reposDir.Dispose();
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();
        repoRoot.Dispose();
        parsed.Dispose();
        return 0;
    }

    private static void EmitRepo(NativeStringView reposDir, NativeStringView spec)
    {
        var cloneName = GitClient.DeriveCloneName(spec);
        var clone = Fs.Combine(reposDir, cloneName.AsView());
        cloneName.Dispose();
        var url = GitClient.BuildCloneUrl(spec);

        var shortName = spec;
        int slash = spec.Bytes.LastIndexOf((byte)'/');
        if (slash >= 0) shortName = new NativeStringView(spec.Bytes[(slash + 1)..]);

        EmitRepoHeader(shortName, url.AsView(), spec);
        url.Dispose();

        if (!Fs.IsDirectory(clone.AsView()))
        {
            ConsoleOut.WriteLineStdout("     (not yet cloned — run `dotai sync`)"u8);
            clone.Dispose();
            return;
        }

        var skills = ListSkillNames(clone.AsView());
        var files = ListRelativeFilePaths(clone.AsView());

        EmitCountLine(skills.Length, files.Length);
        EmitListLine("🧠 skills: "u8, skills.AsView());
        EmitListLine("📁 files:  "u8, files.AsView());

        for (int i = 0; i < skills.Length; i++) skills[i].Dispose();
        skills.Dispose();
        for (int i = 0; i < files.Length; i++) files[i].Dispose();
        files.Dispose();
        clone.Dispose();
    }

    private static NativeList<NativeString> ListSkillNames(NativeStringView clone)
    {
        var dir = Fs.Combine(clone, "skills"u8);
        var list = new NativeList<NativeString>(8);
        if (!Fs.IsDirectory(dir.AsView())) { dir.Dispose(); return list; }
        var entries = Fs.EnumerateDirectories(dir.AsView());
        for (int i = 0; i < entries.Length; i++)
            list.Add(Fs.GetFileName(entries[i].AsView()));
        for (int i = 0; i < entries.Length; i++) entries[i].Dispose();
        entries.Dispose();
        dir.Dispose();
        SortStrings(list);
        return list;
    }

    private static NativeList<NativeString> ListRelativeFilePaths(NativeStringView clone)
    {
        var dir = Fs.Combine(clone, "files"u8);
        var list = new NativeList<NativeString>(8);
        if (!Fs.IsDirectory(dir.AsView())) { dir.Dispose(); return list; }
        var files = Fs.EnumerateFiles(dir.AsView(), recursive: true);
        for (int i = 0; i < files.Length; i++)
        {
            var rel = Fs.GetRelativePath(dir.AsView(), files[i].AsView());
            list.Add(rel);
        }
        for (int i = 0; i < files.Length; i++) files[i].Dispose();
        files.Dispose();
        dir.Dispose();
        SortStrings(list);
        return list;
    }

    private static void SortStrings(NativeList<NativeString> list)
    {
        // In-place insertion sort; lists are tiny.
        for (int i = 1; i < list.Length; i++)
        {
            var cur = list[i];
            int j = i - 1;
            while (j >= 0 && CompareBytes(list[j].AsView().Bytes, cur.AsView().Bytes) > 0)
            {
                list[j + 1] = list[j];
                j--;
            }
            list[j + 1] = cur;
        }
    }

    private static int CompareBytes(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            if (a[i] != b[i]) return a[i] - b[i];
        }
        return a.Length - b.Length;
    }

    private static void EmitRepoHeader(NativeStringView shortName, NativeStringView url, NativeStringView spec)
    {
        bool tty = Stdio.IsTty(1);
        if (tty)
        {
            Stdio.Write(1, "🗂️  "u8);
            Stdio.Write(1, "\x1b]8;;"u8);
            Stdio.Write(1, url);
            Stdio.Write(1, "\x1b\\"u8);
            Stdio.Write(1, "\x1b[36m"u8);
            Stdio.Write(1, shortName);
            Stdio.Write(1, "\x1b[0m"u8);
            Stdio.Write(1, "\x1b]8;;\x1b\\"u8);
            Stdio.Write(1, "  \x1b[90m"u8);
            Stdio.Write(1, spec);
            Stdio.Write(1, "\x1b[0m\n"u8);
        }
        else
        {
            Stdio.Write(1, "# "u8);
            Stdio.Write(1, spec);
            Stdio.Write(1, " ("u8);
            Stdio.Write(1, url);
            Stdio.Write(1, ")\n"u8);
        }
    }

    private static void EmitCountLine(int skills, int files)
    {
        if (skills == 0 && files == 0)
        {
            ConsoleOut.WriteLineStdout("     (no skills or files — repository is empty)"u8);
            return;
        }
        var buf = new NativeBuffer(48);
        buf.Append("     "u8);
        buf.AppendInt(skills);
        buf.Append(skills == 1 ? " skill · "u8 : " skills · "u8);
        buf.AppendInt(files);
        buf.Append(files == 1 ? " file"u8 : " files"u8);
        ConsoleOut.WriteLineStdout(buf.AsView());
        buf.Dispose();
    }

    private static void EmitListLine(NativeStringView label, NativeListView<NativeString> items)
    {
        if (items.Length == 0) return;
        var buf = new NativeBuffer(128);
        buf.Append("     "u8);
        buf.Append(label);
        for (int i = 0; i < items.Length; i++)
        {
            if (i > 0) buf.Append(", "u8);
            buf.Append(items[i].AsView());
        }
        ConsoleOut.WriteLineStdout(buf.AsView());
        buf.Dispose();
    }
}
