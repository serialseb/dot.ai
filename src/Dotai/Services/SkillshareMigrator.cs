using Dotai.Native;
using Dotai.Ui;

namespace Dotai.Services;

public struct MigrationStats
{
    public int Skills;
    public int Files;
    public int Repositories;
}

// Migrates a project-mode Skillshare install into the dotai layout.
// "Project mode" is detected by the presence of .skillshare/config.yaml.
//
// End state:
//   .skillshare/skills/_<raw>/        → .ai/repositories/<host>▸<owner>▸<repo>/
//   .skillshare/<rest>/               → .ai/migration/.skillshare/<rest>/
//   any dotai-managed symlink whose final file is inside .skillshare/ is
//   rewritten to point at the new location.
//
// The original layout is preserved under .ai/migration/ so a future
// dotai init --uninstall can rehydrate Skillshare without data loss.
public static unsafe class SkillshareMigrator
{
    public static bool IsProjectMode(NativeStringView repoRoot)
    {
        var cfg = Fs.Combine(repoRoot, ".skillshare/config.yaml"u8);
        bool ok = Fs.Exists(cfg.AsView());
        cfg.Dispose();
        return ok;
    }

    public static bool TryMigrate(NativeStringView repoRoot, out MigrationStats stats)
    {
        stats = default;
        // Resolve symlinks in the root so it matches what realpath(3) returns
        // for paths inside it (on macOS /var is a symlink to /private/var, so
        // a lexical GetFullPath prefix would never match a realpath'd target).
        if (!Fs.TryResolveRealpath(repoRoot, out var absRoot))
            absRoot = Fs.GetFullPath(repoRoot);
        var skillshareDir = Fs.Combine(absRoot.AsView(), ".skillshare"u8);
        var aiDir = Fs.Combine(absRoot.AsView(), ".ai"u8);
        var migrationDir = Fs.Combine(aiDir.AsView(), "migration"u8);
        var movedSkillshare = Fs.Combine(migrationDir.AsView(), ".skillshare"u8);
        var reposDir = Fs.Combine(aiDir.AsView(), "repositories"u8);

        bool ok = false;
        try
        {
            if (Fs.Exists(migrationDir.AsView()))
            {
                ConsoleOut.Error(".ai/migration already exists; refusing to migrate"u8);
                return false;
            }

            // Pass 1: scan siblings (symlinks into .skillshare/).
            var siblings = CollectSiblings(absRoot.AsView(), skillshareDir.AsView());

            // Pass 2: map each .skillshare/skills/_<raw> → .ai/repositories/<new>.
            var repoMoves = PlanRepositoryMoves(skillshareDir.AsView(), reposDir.AsView());

            // Create destination trees.
            Fs.TryCreateDirectory(migrationDir.AsView());
            Fs.TryCreateDirectory(reposDir.AsView());

            // Apply repository moves first so the residual .skillshare/ can be
            // moved wholesale afterward.
            for (int i = 0; i < repoMoves.Length; i++)
                Fs.TryRename(repoMoves[i].OldAbs.AsView(), repoMoves[i].NewAbs.AsView());

            // Move everything left of .skillshare/ into .ai/migration/.
            Fs.TryRename(skillshareDir.AsView(), movedSkillshare.AsView());

            // Rewrite each sibling to the post-migration absolute target.
            int skillsRemapped = 0, filesRemapped = 0;
            var rewritten = new NativeList<Rewrite>(siblings.Length);
            for (int i = 0; i < siblings.Length; i++)
            {
                if (!Remap(siblings[i].Real.AsView(), repoMoves.AsView(),
                    skillshareDir.AsView(), movedSkillshare.AsView(), out var newAbs))
                    continue;

                if (!ReplaceSymlink(siblings[i].Path.AsView(), newAbs.AsView()))
                {
                    newAbs.Dispose();
                    continue;
                }

                rewritten.Add(new Rewrite
                {
                    Path = NativeString.From(siblings[i].Path.AsView()),
                    Former = NativeString.From(siblings[i].OriginalTarget.AsView())
                });
                if (siblings[i].IsSkill) skillsRemapped++; else filesRemapped++;
                newAbs.Dispose();
            }

            // Register each moved repository in .ai/config.toml and write
            // skillshare.toml in the migration folder.
            RegisterInConfig(aiDir.AsView(), repoMoves.AsView());
            WriteSkillshareToml(migrationDir.AsView(), absRoot.AsView(),
                repoMoves.AsView(), rewritten.AsView());

            stats.Skills = skillsRemapped;
            stats.Files = filesRemapped;
            stats.Repositories = repoMoves.Length;

            for (int i = 0; i < siblings.Length; i++) siblings[i].Dispose();
            siblings.Dispose();
            for (int i = 0; i < repoMoves.Length; i++) repoMoves[i].Dispose();
            repoMoves.Dispose();
            for (int i = 0; i < rewritten.Length; i++) rewritten[i].Dispose();
            rewritten.Dispose();

            ok = true;
            return true;
        }
        finally
        {
            if (!ok)
            {
                // Best-effort; caller sees error message.
            }
            absRoot.Dispose();
            skillshareDir.Dispose();
            aiDir.Dispose();
            migrationDir.Dispose();
            movedSkillshare.Dispose();
            reposDir.Dispose();
        }
    }

    // ── sibling scan ─────────────────────────────────────────────────────────

    private struct Sibling
    {
        public NativeString Path;             // absolute path of the symlink
        public NativeString OriginalTarget;   // verbatim readlink output
        public NativeString Real;             // realpath of the symlink
        public bool IsSkill;                  // sibling is inside a skills/ dir
        public void Dispose() { Path.Dispose(); OriginalTarget.Dispose(); Real.Dispose(); }
    }

    private static NativeList<Sibling> CollectSiblings(NativeStringView absRoot,
        NativeStringView skillshareDir)
    {
        var result = new NativeList<Sibling>(16);
        var dotDirs = EnumerateDotDirectories(absRoot);
        for (int i = 0; i < dotDirs.Length; i++)
        {
            var skillsDir = Fs.Combine(dotDirs[i].AsView(), "skills"u8);
            if (Fs.IsDirectory(skillsDir.AsView()))
                CollectSymlinksInto(skillsDir.AsView(), skillshareDir, isSkill: true, ref result);
            skillsDir.Dispose();
        }
        for (int i = 0; i < dotDirs.Length; i++) dotDirs[i].Dispose();
        dotDirs.Dispose();

        var topEntries = Fs.EnumerateFileSystemEntries(absRoot);
        for (int i = 0; i < topEntries.Length; i++)
            MaybeCollect(topEntries[i].AsView(), skillshareDir, isSkill: false, ref result);
        for (int i = 0; i < topEntries.Length; i++) topEntries[i].Dispose();
        topEntries.Dispose();

        return result;
    }

    private static NativeList<NativeString> EnumerateDotDirectories(NativeStringView absRoot)
    {
        var all = Fs.EnumerateDirectories(absRoot);
        var filtered = new NativeList<NativeString>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            var name = Fs.GetFileName(all[i].AsView());
            bool keep = name.AsView().Length > 0 && name.AsView().Bytes[0] == (byte)'.'
                && name.AsView().Bytes.SequenceEqual(".skillshare"u8) == false
                && name.AsView().Bytes.SequenceEqual(".ai"u8) == false
                && name.AsView().Bytes.SequenceEqual(".git"u8) == false;
            if (keep) filtered.Add(NativeString.From(all[i].AsView()));
            name.Dispose();
            all[i].Dispose();
        }
        all.Dispose();
        return filtered;
    }

    private static void CollectSymlinksInto(NativeStringView dir, NativeStringView skillshareDir,
        bool isSkill, ref NativeList<Sibling> into)
    {
        var entries = Fs.EnumerateFileSystemEntries(dir);
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i].AsView();
            if (Fs.IsSymlink(entry))
            {
                MaybeCollect(entry, skillshareDir, isSkill, ref into);
            }
            else if (Fs.IsDirectory(entry))
            {
                CollectSymlinksInto(entry, skillshareDir, isSkill, ref into);
            }
        }
        for (int i = 0; i < entries.Length; i++) entries[i].Dispose();
        entries.Dispose();
    }

    private static void MaybeCollect(NativeStringView entry, NativeStringView skillshareDir,
        bool isSkill, ref NativeList<Sibling> into)
    {
        if (!Fs.IsSymlink(entry)) return;
        if (!Fs.TryReadSymbolicLinkTarget(entry, out var origTarget)) return;
        if (!Fs.TryResolveRealpath(entry, out var real))
        {
            origTarget.Dispose();
            return;
        }
        if (!real.AsView().Bytes.StartsWith(skillshareDir.Bytes)
            || !HasDirectoryBoundaryAfter(real.AsView(), skillshareDir.Length))
        {
            origTarget.Dispose();
            real.Dispose();
            return;
        }
        into.Add(new Sibling
        {
            Path = NativeString.From(entry),
            OriginalTarget = origTarget,
            Real = real,
            IsSkill = isSkill
        });
    }

    // ── repository planning ──────────────────────────────────────────────────

    private struct RepoMove
    {
        public NativeString RawName;    // "_origin" or similar
        public NativeString OldAbs;     // .skillshare/skills/_origin
        public NativeString NewAbs;     // .ai/repositories/github.com▸owner▸repo
        public NativeString Spec;       // "owner/repo" or "host/owner/repo"
        public NativeString NewName;    // "github.com▸owner▸repo"
        public void Dispose()
        {
            RawName.Dispose(); OldAbs.Dispose(); NewAbs.Dispose();
            Spec.Dispose(); NewName.Dispose();
        }
    }

    private static NativeList<RepoMove> PlanRepositoryMoves(NativeStringView skillshareDir,
        NativeStringView reposDir)
    {
        var skillsDir = Fs.Combine(skillshareDir, "skills"u8);
        var moves = new NativeList<RepoMove>(4);
        if (!Fs.IsDirectory(skillsDir.AsView())) { skillsDir.Dispose(); return moves; }

        var entries = Fs.EnumerateDirectories(skillsDir.AsView());
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i].AsView();
            var name = Fs.GetFileName(entry);
            if (name.AsView().Length == 0 || name.AsView().Bytes[0] != (byte)'_')
            {
                name.Dispose();
                continue;
            }
            var spec = DeriveSpecFromClone(entry, name.AsView());
            var newName = GitClient.DeriveCloneName(spec.AsView());
            var newAbs = Fs.Combine(reposDir, newName.AsView());
            moves.Add(new RepoMove
            {
                RawName = name,
                OldAbs = NativeString.From(entry),
                NewAbs = newAbs,
                Spec = spec,
                NewName = newName
            });
        }
        for (int i = 0; i < entries.Length; i++) entries[i].Dispose();
        entries.Dispose();
        skillsDir.Dispose();
        return moves;
    }

    // Derives a dotai repo spec from a Skillshare clone directory. Prefers
    // the origin URL recorded in .git/config; falls back to the directory
    // name when unavailable, using 'local' as the host to flag ambiguity.
    private static NativeString DeriveSpecFromClone(NativeStringView cloneDir, NativeStringView rawName)
    {
        var cfg = Fs.Combine(cloneDir, ".git/config"u8);
        if (Fs.Exists(cfg.AsView()) && Fs.TryReadAllBytes(cfg.AsView(), out var bytes))
        {
            var url = ExtractOriginUrl(bytes.AsView());
            bytes.Dispose();
            cfg.Dispose();
            if (!url.IsEmpty)
            {
                var spec = SpecFromUrl(url.AsView());
                url.Dispose();
                if (!spec.IsEmpty) return spec;
            }
        }
        else
        {
            cfg.Dispose();
        }

        // Fallback: treat "_<owner>_<repo>" or "_<name>" as local/<owner>/<repo>.
        var body = rawName.Bytes;
        while (!body.IsEmpty && body[0] == (byte)'_') body = body[1..];
        int sep = body.IndexOf((byte)'_');
        var buf = new NativeBuffer(rawName.Length + 16);
        buf.Append("local/"u8);
        if (sep > 0)
        {
            buf.Append(new NativeStringView(body[..sep]));
            buf.AppendByte((byte)'/');
            buf.Append(new NativeStringView(body[(sep + 1)..]));
        }
        else
        {
            // No clear owner: duplicate the name so we still get a 3-segment spec.
            buf.Append(new NativeStringView(body));
            buf.AppendByte((byte)'/');
            buf.Append(new NativeStringView(body));
        }
        return buf.Freeze();
    }

    private static NativeString ExtractOriginUrl(NativeStringView cfg)
    {
        var bytes = cfg.Bytes;
        int idx = bytes.IndexOf("[remote \"origin\"]"u8);
        if (idx < 0) return default;
        var rest = bytes[(idx + "[remote \"origin\"]"u8.Length)..];
        while (!rest.IsEmpty)
        {
            int nl = rest.IndexOf((byte)'\n');
            var line = nl < 0 ? rest : rest[..nl];
            var trimmed = new NativeStringView(line).Trim();
            if (!trimmed.IsEmpty)
            {
                if (trimmed.Bytes[0] == (byte)'[') break; // next section
                int eq = trimmed.IndexOf((byte)'=');
                if (eq > 0)
                {
                    var key = new NativeStringView(trimmed.Bytes[..eq]).Trim();
                    if (key.Bytes.SequenceEqual("url"u8))
                    {
                        var val = new NativeStringView(trimmed.Bytes[(eq + 1)..]).Trim();
                        return NativeString.From(val);
                    }
                }
            }
            if (nl < 0) break;
            rest = rest[(nl + 1)..];
        }
        return default;
    }

    // Parses https://host[:port]/owner/repo(.git)? into a dotai spec
    // ("host/owner/repo" or "owner/repo" for github.com). Returns empty on
    // anything else (ssh, git://, malformed) so the caller falls back.
    private static NativeString SpecFromUrl(NativeStringView url)
    {
        var bytes = url.Bytes;
        ReadOnlySpan<byte> scheme = "https://"u8;
        if (!bytes.StartsWith(scheme)) return default;
        var tail = bytes[scheme.Length..];
        int slash = tail.IndexOf((byte)'/');
        if (slash <= 0) return default;
        var host = tail[..slash];
        var path = tail[(slash + 1)..];
        while (!path.IsEmpty && path[^1] == (byte)'/') path = path[..^1];
        if (path.Length >= 4 && new NativeStringView(path).EndsWith(".git"u8))
            path = path[..^4];
        int firstPathSlash = path.IndexOf((byte)'/');
        if (firstPathSlash <= 0 || firstPathSlash == path.Length - 1) return default;

        var buf = new NativeBuffer(bytes.Length);
        if (!host.SequenceEqual("github.com"u8))
        {
            buf.Append(new NativeStringView(host));
            buf.AppendByte((byte)'/');
        }
        buf.Append(new NativeStringView(path));
        return buf.Freeze();
    }

    // ── sibling remap ────────────────────────────────────────────────────────

    private static bool Remap(NativeStringView realTarget, NativeListView<RepoMove> moves,
        NativeStringView skillshareDir, NativeStringView movedSkillshare, out NativeString newAbs)
    {
        for (int i = 0; i < moves.Length; i++)
        {
            var oldAbs = moves[i].OldAbs.AsView();
            if (realTarget.Bytes.StartsWith(oldAbs.Bytes)
                && HasDirectoryBoundaryAfter(realTarget, oldAbs.Length))
            {
                var tail = realTarget.Bytes[oldAbs.Length..];
                var buf = new NativeBuffer(moves[i].NewAbs.Length + tail.Length);
                buf.Append(moves[i].NewAbs.AsView());
                buf.Append(new NativeStringView(tail));
                newAbs = buf.Freeze();
                return true;
            }
        }

        if (realTarget.Bytes.StartsWith(skillshareDir.Bytes)
            && HasDirectoryBoundaryAfter(realTarget, skillshareDir.Length))
        {
            var tail = realTarget.Bytes[skillshareDir.Length..];
            var buf = new NativeBuffer(movedSkillshare.Length + tail.Length);
            buf.Append(movedSkillshare);
            buf.Append(new NativeStringView(tail));
            newAbs = buf.Freeze();
            return true;
        }

        newAbs = default;
        return false;
    }

    private static bool ReplaceSymlink(NativeStringView link, NativeStringView targetAbs)
    {
        Fs.TryDeleteFile(link);
        var dirName = Fs.GetDirectoryName(link);
        var rel = Fs.GetRelativePath(dirName.AsView(), targetAbs);
        dirName.Dispose();
        bool ok = Fs.TryCreateSymbolicLink(link, rel.AsView());
        rel.Dispose();
        return ok;
    }

    // ── config / toml writers ────────────────────────────────────────────────

    private static void RegisterInConfig(NativeStringView aiDir, NativeListView<RepoMove> moves)
    {
        var configPath = Fs.Combine(aiDir, "config.toml"u8);
        ConfigStore.TryLoad(configPath.AsView(), out var config);
        for (int i = 0; i < moves.Length; i++)
        {
            if (!ConfigStore.Contains(config.AsView(), moves[i].Spec.AsView()))
                ConfigStore.AddRepo(ref config, moves[i].Spec.AsView(), "merge"u8);
        }
        ConfigStore.Save(configPath.AsView(), config.AsView());
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();
        configPath.Dispose();
    }

    private struct Rewrite
    {
        public NativeString Path;
        public NativeString Former;
        public void Dispose() { Path.Dispose(); Former.Dispose(); }
    }

    private static void WriteSkillshareToml(NativeStringView migrationDir, NativeStringView absRoot,
        NativeListView<RepoMove> moves, NativeListView<Rewrite> rewrites)
    {
        var buf = new NativeBuffer(512);
        buf.Append("# skillshare migration record — describes how dotai rewired a\n"u8);
        buf.Append("# Skillshare project-mode install so it can be rolled back.\n\n"u8);
        for (int i = 0; i < moves.Length; i++)
        {
            buf.Append("[[repository]]\n"u8);
            buf.Append("new_name = \""u8);
            buf.Append(moves[i].NewName.AsView());
            buf.Append("\"\nformer_path = \""u8);
            var rel = Fs.GetRelativePath(absRoot, moves[i].OldAbs.AsView());
            buf.Append(rel.AsView());
            rel.Dispose();
            buf.Append("\"\n\n"u8);
        }
        for (int i = 0; i < rewrites.Length; i++)
        {
            buf.Append("[[symlink]]\n"u8);
            buf.Append("path = \""u8);
            var relPath = Fs.GetRelativePath(absRoot, rewrites[i].Path.AsView());
            buf.Append(relPath.AsView());
            relPath.Dispose();
            buf.Append("\"\nformer_target = \""u8);
            buf.Append(rewrites[i].Former.AsView());
            buf.Append("\"\n\n"u8);
        }
        var outPath = Fs.Combine(migrationDir, "skillshare.toml"u8);
        Fs.TryWriteAllBytes(outPath.AsView(), buf.AsView());
        outPath.Dispose();
        buf.Dispose();
    }

    // ── shared helpers ───────────────────────────────────────────────────────

    private static bool HasDirectoryBoundaryAfter(NativeStringView path, int prefixLen)
    {
        if (path.Length == prefixLen) return true;
        return path.Bytes[prefixLen] == (byte)'/';
    }
}
