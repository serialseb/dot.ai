using System.Text;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Dotai.Text;
using Xunit;

namespace Dotai.Tests;

public class SkillLinkerTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);
    private static Arg[] Agents(params string[] names)
    {
        var result = new Arg[names.Length];
        for (int i = 0; i < names.Length; i++) result[i] = new Arg(B(names[i]));
        return result;
    }

    private static string MakeClone(string baseDir, string name, Action<string> build)
    {
        var clone = Path.Combine(baseDir, ".ai", "repositories", name);
        Directory.CreateDirectory(clone);
        build(clone);
        return clone;
    }

    [Fact]
    public void LinksSkillsIntoAgentDir()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".claude"));
        var clone = MakeClone(tmp.Path, "owner_repo", c =>
        {
            Directory.CreateDirectory(Path.Combine(c, "skills", "alpha"));
            File.WriteAllText(Path.Combine(c, "skills", "alpha", "SKILL.md"), "hello");
        });
        var report = new SyncReport();

        SkillLinker.LinkSkills((FastString)B(tmp.Path), (FastString)B(clone), Agents(".claude"), report);

        var link = Path.Combine(tmp.Path, ".claude", "skills", "alpha");
        Assert.True(Directory.Exists(link));
        Assert.NotNull(new FileInfo(link).LinkTarget);
    }

    [Fact]
    public void LinksFilesPreservingHierarchy()
    {
        using var tmp = new TempDir();
        var clone = MakeClone(tmp.Path, "owner_repo", c =>
        {
            var nested = Path.Combine(c, "files", "config");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "app.yaml"), "x: 1");
        });
        var report = new SyncReport();

        SkillLinker.LinkFiles((FastString)B(tmp.Path), (FastString)B(clone), report);

        var link = Path.Combine(tmp.Path, "config", "app.yaml");
        var info = new FileInfo(link);
        Assert.True(info.Exists);
        Assert.NotNull(info.LinkTarget);
    }

    [Fact]
    public void CollisionAcrossClonesIsReported()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".claude"));
        var cloneA = MakeClone(tmp.Path, "a_repo", c =>
        {
            Directory.CreateDirectory(Path.Combine(c, "skills", "alpha"));
        });
        var cloneB = MakeClone(tmp.Path, "b_repo", c =>
        {
            Directory.CreateDirectory(Path.Combine(c, "skills", "alpha"));
        });
        var report = new SyncReport();

        SkillLinker.LinkSkills((FastString)B(tmp.Path), (FastString)B(cloneA), Agents(".claude"), report);
        SkillLinker.LinkSkills((FastString)B(tmp.Path), (FastString)B(cloneB), Agents(".claude"), report);

        Assert.NotEmpty(report.Conflicts);
        var link = new FileInfo(Path.Combine(tmp.Path, ".claude", "skills", "alpha"));
        Assert.Contains(cloneA, link.LinkTarget);
    }

    [Fact]
    public void OrphanCleanupRemovesDanglingDotaiOwnedLinks()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".claude"));
        var clone = MakeClone(tmp.Path, "owner_repo", c =>
        {
            Directory.CreateDirectory(Path.Combine(c, "skills", "alpha"));
        });
        var report = new SyncReport();
        SkillLinker.LinkSkills((FastString)B(tmp.Path), (FastString)B(clone), Agents(".claude"), report);

        Directory.Delete(Path.Combine(clone, "skills", "alpha"), recursive: true);
        SkillLinker.CleanupOrphans((FastString)B(tmp.Path), Agents(".claude"));

        Assert.False(File.Exists(Path.Combine(tmp.Path, ".claude", "skills", "alpha")));
    }

    [Fact]
    public void OrphanCleanupLeavesUserOwnedLinksAlone()
    {
        using var tmp = new TempDir();
        var agent = Path.Combine(tmp.Path, ".claude", "skills");
        Directory.CreateDirectory(agent);
        var userTarget = Path.Combine(tmp.Path, "user-skill");
        Directory.CreateDirectory(userTarget);
        File.CreateSymbolicLink(Path.Combine(agent, "user"), userTarget);
        Directory.Delete(userTarget, recursive: true);

        SkillLinker.CleanupOrphans((FastString)B(tmp.Path), Agents(".claude"));

        Assert.True(new FileInfo(Path.Combine(agent, "user")).LinkTarget != null);
    }

    [Fact]
    public void LinkSkillsCountsNewSymlinks()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".claude"));
        var clone = MakeClone(tmp.Path, "owner_repo", c =>
        {
            Directory.CreateDirectory(Path.Combine(c, "skills", "alpha"));
            Directory.CreateDirectory(Path.Combine(c, "skills", "beta"));
        });
        var report = new SyncReport();

        SkillLinker.LinkSkills((FastString)B(tmp.Path), (FastString)B(clone), Agents(".claude"), report);

        Assert.Equal(2, report.SkillsLinked);
    }

    [Fact]
    public void LinkFilesCountsNewSymlinks()
    {
        using var tmp = new TempDir();
        var clone = MakeClone(tmp.Path, "owner_repo", c =>
        {
            var files = Path.Combine(c, "files");
            Directory.CreateDirectory(files);
            File.WriteAllText(Path.Combine(files, "one.txt"), "1");
            Directory.CreateDirectory(Path.Combine(files, "sub"));
            File.WriteAllText(Path.Combine(files, "sub", "two.txt"), "2");
        });
        var report = new SyncReport();

        SkillLinker.LinkFiles((FastString)B(tmp.Path), (FastString)B(clone), report);

        Assert.Equal(2, report.FilesLinked);
    }

    [Fact]
    public void ForceResetRemovesOwnedSkillSymlinks()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".claude"));
        var clone = MakeClone(tmp.Path, "owner_repo", c =>
        {
            Directory.CreateDirectory(Path.Combine(c, "skills", "alpha"));
        });
        var report = new SyncReport();
        SkillLinker.LinkSkills((FastString)B(tmp.Path), (FastString)B(clone), Agents(".claude"), report);

        SkillLinker.ForceReset((FastString)B(tmp.Path), Agents(".claude"));

        Assert.False(Directory.Exists(Path.Combine(tmp.Path, ".claude", "skills", "alpha")));
    }

    [Fact]
    public void ForceResetLeavesUserOwnedSymlinksAlone()
    {
        using var tmp = new TempDir();
        var agent = Path.Combine(tmp.Path, ".claude", "skills");
        Directory.CreateDirectory(agent);
        var userTarget = Path.Combine(tmp.Path, "user-skill");
        Directory.CreateDirectory(userTarget);
        File.CreateSymbolicLink(Path.Combine(agent, "user"), userTarget);

        SkillLinker.ForceReset((FastString)B(tmp.Path), Agents(".claude"));

        Assert.True(new FileInfo(Path.Combine(agent, "user")).LinkTarget != null);
    }

    [Fact]
    public void ForceResetRemovesOwnedFileSymlinks()
    {
        using var tmp = new TempDir();
        var clone = MakeClone(tmp.Path, "owner_repo", c =>
        {
            var files = Path.Combine(c, "files");
            Directory.CreateDirectory(files);
            File.WriteAllText(Path.Combine(files, "one.txt"), "1");
        });
        var report = new SyncReport();
        SkillLinker.LinkFiles((FastString)B(tmp.Path), (FastString)B(clone), report);
        Assert.True(File.Exists(Path.Combine(tmp.Path, "one.txt")));

        SkillLinker.ForceReset((FastString)B(tmp.Path), Array.Empty<Arg>());

        Assert.False(File.Exists(Path.Combine(tmp.Path, "one.txt")));
    }
}
