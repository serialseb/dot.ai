using System.Text;
using Dotai.Native;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class SkillshareMigratorTests
{
    private static NativeStringView V(string s) => Encoding.UTF8.GetBytes(s);

    private static string MakeSkillshareProject(string baseDir, string remoteUrl,
        string ownerSlashRepo = "foo/bar")
    {
        var repo = Path.Combine(baseDir, "project");
        Directory.CreateDirectory(repo);
        LocalGitRepo.Run(repo, "init", "--initial-branch=main");

        // Skillshare layout (project mode).
        var ss = Path.Combine(repo, ".skillshare");
        var ssSkills = Path.Combine(ss, "skills");
        Directory.CreateDirectory(ssSkills);
        File.WriteAllText(Path.Combine(ss, "config.yaml"),
            $"source: https://github.com/{ownerSlashRepo}\nmode: merge\n");

        var origin = Path.Combine(ssSkills, "_origin");
        LocalGitRepo.Run(baseDir, "clone", remoteUrl, origin);
        // Simulate the production origin URL that Skillshare records even
        // though the test clones from a local file:// remote.
        LocalGitRepo.Run(origin, "remote", "set-url", "origin", $"https://github.com/{ownerSlashRepo}");

        // Claude skill sibling pointing into the cloned repo.
        var claudeSkills = Path.Combine(repo, ".claude", "skills");
        Directory.CreateDirectory(claudeSkills);
        var claudeAlpha = Path.Combine(claudeSkills, "alpha");
        File.CreateSymbolicLink(claudeAlpha,
            Path.GetRelativePath(claudeSkills, Path.Combine(origin, "skills", "alpha")));

        // Cursor skill sibling (second dot-dir with a skills/ folder).
        var cursorSkills = Path.Combine(repo, ".cursor", "skills");
        Directory.CreateDirectory(cursorSkills);
        File.CreateSymbolicLink(Path.Combine(cursorSkills, "alpha"),
            Path.GetRelativePath(cursorSkills, Path.Combine(origin, "skills", "alpha")));

        // Top-level AGENTS.shared.md via the extras symlink chain that
        // Skillshare's install.sh sets up:
        //   .skillshare/extras → skills/_origin/extras
        //   AGENTS.shared.md  → .skillshare/extras/agents/AGENTS.shared.md
        File.CreateSymbolicLink(Path.Combine(ss, "extras"),
            Path.Combine("skills", "_origin", "extras"));
        File.CreateSymbolicLink(Path.Combine(repo, "AGENTS.shared.md"),
            Path.Combine(".skillshare", "extras", "agents", "AGENTS.shared.md"));

        return repo;
    }

    [Fact]
    public void DetectsProjectModeByConfigYaml()
    {
        using var tmp = new TempDir();
        var repo = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(Path.Combine(repo, ".skillshare"));
        Assert.False(SkillshareMigrator.IsProjectMode(V(repo)));
        File.WriteAllText(Path.Combine(repo, ".skillshare", "config.yaml"), "source: x\n");
        Assert.True(SkillshareMigrator.IsProjectMode(V(repo)));
    }

    [Fact]
    public void MovesRepositoryAndRewritesSiblings()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
        {
            var skill = Path.Combine(w, "skills", "alpha");
            Directory.CreateDirectory(skill);
            File.WriteAllText(Path.Combine(skill, "SKILL.md"), "hi");

            var extra = Path.Combine(w, "extras", "agents");
            Directory.CreateDirectory(extra);
            File.WriteAllText(Path.Combine(extra, "AGENTS.shared.md"), "shared");
        });
        var repo = MakeSkillshareProject(tmp.Path, remoteUrl);

        Assert.True(SkillshareMigrator.TryMigrate(V(repo), out var stats));
        Assert.Equal(1, stats.Repositories);
        Assert.Equal(2, stats.Skills); // .claude and .cursor
        Assert.Equal(1, stats.Files);  // AGENTS.shared.md

        // Repository moved under the new naming scheme.
        var newRepoDir = Path.Combine(repo, ".ai", "repositories", "github.com▸foo▸bar");
        Assert.True(Directory.Exists(Path.Combine(newRepoDir, ".git")));
        Assert.True(File.Exists(Path.Combine(newRepoDir, "skills", "alpha", "SKILL.md")));

        // .skillshare archived.
        Assert.False(Directory.Exists(Path.Combine(repo, ".skillshare")));
        Assert.True(Directory.Exists(Path.Combine(repo, ".ai", "migration", ".skillshare")));
        Assert.True(File.Exists(Path.Combine(repo, ".ai", "migration", "skillshare.toml")));

        // Siblings now resolve to the new repository location.
        var claudeAlpha = Path.Combine(repo, ".claude", "skills", "alpha");
        Assert.NotNull(new FileInfo(claudeAlpha).LinkTarget);
        Assert.True(File.Exists(Path.Combine(claudeAlpha, "SKILL.md")));

        var topAgent = Path.Combine(repo, "AGENTS.shared.md");
        Assert.NotNull(new FileInfo(topAgent).LinkTarget);
        Assert.True(File.Exists(topAgent));
        Assert.Equal("shared", File.ReadAllText(topAgent));

        // Config has the repo registered under the canonical spec.
        var config = File.ReadAllText(Path.Combine(repo, ".ai", "config.toml"));
        Assert.Contains("\"foo/bar\"", config);

        // skillshare.toml records the former layout for rollback.
        var ssToml = File.ReadAllText(Path.Combine(repo, ".ai", "migration", "skillshare.toml"));
        Assert.Contains("[[repository]]", ssToml);
        Assert.Contains("new_name = \"github.com▸foo▸bar\"", ssToml);
        Assert.Contains("former_path = \".skillshare/skills/_origin\"", ssToml);
        Assert.Contains("[[symlink]]", ssToml);
    }

    [Fact]
    public void RefusesWhenMigrationDirAlreadyExists()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
            File.WriteAllText(Path.Combine(w, "readme.md"), "x"));
        var repo = MakeSkillshareProject(tmp.Path, remoteUrl);
        Directory.CreateDirectory(Path.Combine(repo, ".ai", "migration"));

        Assert.False(SkillshareMigrator.TryMigrate(V(repo), out _));
        // .skillshare untouched.
        Assert.True(Directory.Exists(Path.Combine(repo, ".skillshare")));
    }
}
