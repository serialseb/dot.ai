using System.Text;
using Dotai.Native;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class SkillLinkerTests
{
    private static NativeStringView V(string s) => Encoding.UTF8.GetBytes(s);

    private static NativeList<NativeString> Agents(params string[] names)
    {
        var result = new NativeList<NativeString>(names.Length > 0 ? names.Length : 1);
        for (int i = 0; i < names.Length; i++)
            result.Add(NativeString.From(V(names[i])));
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
        var report = new SyncReport(4);
        var agents = Agents(".claude");

        SkillLinker.LinkSkills(V(tmp.Path), V(clone), agents.AsView(), ref report);

        var link = Path.Combine(tmp.Path, ".claude", "skills", "alpha");
        Assert.True(Directory.Exists(link));
        Assert.NotNull(new FileInfo(link).LinkTarget);
        report.Dispose();
        for (int i = 0; i < agents.Length; i++) agents[i].Dispose();
        agents.Dispose();
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
        var report = new SyncReport(4);

        SkillLinker.LinkFiles(V(tmp.Path), V(clone), ref report);

        var link = Path.Combine(tmp.Path, "config", "app.yaml");
        var info = new FileInfo(link);
        Assert.True(info.Exists);
        Assert.NotNull(info.LinkTarget);
        report.Dispose();
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
        var report = new SyncReport(4);
        var agents = Agents(".claude");

        SkillLinker.LinkSkills(V(tmp.Path), V(cloneA), agents.AsView(), ref report);
        SkillLinker.LinkSkills(V(tmp.Path), V(cloneB), agents.AsView(), ref report);

        Assert.True(report.Conflicts.Length > 0);
        var link = new FileInfo(Path.Combine(tmp.Path, ".claude", "skills", "alpha"));
        var resolved = Path.GetFullPath(link.LinkTarget!, Path.GetDirectoryName(link.FullName)!);
        Assert.StartsWith(cloneA, resolved);
        report.Dispose();
        for (int i = 0; i < agents.Length; i++) agents[i].Dispose();
        agents.Dispose();
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
        var report = new SyncReport(4);
        var agents = Agents(".claude");
        SkillLinker.LinkSkills(V(tmp.Path), V(clone), agents.AsView(), ref report);
        report.Dispose();

        Directory.Delete(Path.Combine(clone, "skills", "alpha"), recursive: true);
        SkillLinker.CleanupOrphans(V(tmp.Path), agents.AsView());

        Assert.False(File.Exists(Path.Combine(tmp.Path, ".claude", "skills", "alpha")));
        for (int i = 0; i < agents.Length; i++) agents[i].Dispose();
        agents.Dispose();
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
        var agents = Agents(".claude");

        SkillLinker.CleanupOrphans(V(tmp.Path), agents.AsView());

        Assert.True(new FileInfo(Path.Combine(agent, "user")).LinkTarget != null);
        for (int i = 0; i < agents.Length; i++) agents[i].Dispose();
        agents.Dispose();
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
        var report = new SyncReport(4);
        var agents = Agents(".claude");

        SkillLinker.LinkSkills(V(tmp.Path), V(clone), agents.AsView(), ref report);

        Assert.Equal(2, report.SkillsLinked);
        report.Dispose();
        for (int i = 0; i < agents.Length; i++) agents[i].Dispose();
        agents.Dispose();
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
        var report = new SyncReport(4);

        SkillLinker.LinkFiles(V(tmp.Path), V(clone), ref report);

        Assert.Equal(2, report.FilesLinked);
        report.Dispose();
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
        var report = new SyncReport(4);
        var agents = Agents(".claude");
        SkillLinker.LinkSkills(V(tmp.Path), V(clone), agents.AsView(), ref report);
        report.Dispose();

        SkillLinker.ForceReset(V(tmp.Path), agents.AsView());

        Assert.False(Directory.Exists(Path.Combine(tmp.Path, ".claude", "skills", "alpha")));
        for (int i = 0; i < agents.Length; i++) agents[i].Dispose();
        agents.Dispose();
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
        var agents = Agents(".claude");

        SkillLinker.ForceReset(V(tmp.Path), agents.AsView());

        Assert.True(new FileInfo(Path.Combine(agent, "user")).LinkTarget != null);
        for (int i = 0; i < agents.Length; i++) agents[i].Dispose();
        agents.Dispose();
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
        var report = new SyncReport(4);
        SkillLinker.LinkFiles(V(tmp.Path), V(clone), ref report);
        Assert.True(File.Exists(Path.Combine(tmp.Path, "one.txt")));
        report.Dispose();

        var emptyAgents = new NativeList<NativeString>(0);
        // Note: NativeList(0) → capacity < 4 → uses 4 internally, but Length is 0
        // We need a valid NativeList even if empty
        SkillLinker.ForceReset(V(tmp.Path), emptyAgents.AsView());
        emptyAgents.Dispose();

        Assert.False(File.Exists(Path.Combine(tmp.Path, "one.txt")));
    }

    [Fact]
    public void LinksUseRelativeTargets()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".claude"));
        var clone = MakeClone(tmp.Path, "owner_repo", c =>
        {
            Directory.CreateDirectory(Path.Combine(c, "skills", "alpha"));
        });
        var report = new SyncReport(4);
        var agents = Agents(".claude");
        SkillLinker.LinkSkills(V(tmp.Path), V(clone), agents.AsView(), ref report);

        var link = new FileInfo(Path.Combine(tmp.Path, ".claude", "skills", "alpha"));
        Assert.NotNull(link.LinkTarget);
        Assert.False(Path.IsPathRooted(link.LinkTarget), $"expected relative, got '{link.LinkTarget}'");
        var resolved = Path.GetFullPath(link.LinkTarget!, Path.GetDirectoryName(link.FullName)!);
        Assert.Equal(Path.Combine(clone, "skills", "alpha"), resolved);

        report.Dispose();
        for (int i = 0; i < agents.Length; i++) agents[i].Dispose();
        agents.Dispose();
    }

    [Fact]
    public void ForceBacksUpConcreteFileAndReplacesWithSymlink()
    {
        using var tmp = new TempDir();
        var clone = MakeClone(tmp.Path, "owner_repo", c =>
        {
            var files = Path.Combine(c, "files");
            Directory.CreateDirectory(files);
            File.WriteAllText(Path.Combine(files, "one.txt"), "source");
        });
        var concrete = Path.Combine(tmp.Path, "one.txt");
        File.WriteAllText(concrete, "user-local");

        var report = new SyncReport(4);
        SkillLinker.LinkFiles(V(tmp.Path), V(clone), ref report, force: true);

        Assert.True(File.Exists(concrete));
        Assert.NotNull(new FileInfo(concrete).LinkTarget);
        var bak = concrete + ".bak";
        Assert.True(File.Exists(bak));
        Assert.Equal("user-local", File.ReadAllText(bak));
        Assert.Equal(0, report.Conflicts.Length);
        report.Dispose();
    }

    [Fact]
    public void NoForceConcreteFileReportsConflict()
    {
        using var tmp = new TempDir();
        var clone = MakeClone(tmp.Path, "owner_repo", c =>
        {
            var files = Path.Combine(c, "files");
            Directory.CreateDirectory(files);
            File.WriteAllText(Path.Combine(files, "one.txt"), "source");
        });
        var concrete = Path.Combine(tmp.Path, "one.txt");
        File.WriteAllText(concrete, "user-local");

        var report = new SyncReport(4);
        SkillLinker.LinkFiles(V(tmp.Path), V(clone), ref report);

        Assert.Null(new FileInfo(concrete).LinkTarget);
        Assert.Equal("user-local", File.ReadAllText(concrete));
        Assert.False(File.Exists(concrete + ".bak"));
        Assert.True(report.Conflicts.Length > 0);
        report.Dispose();
    }
}
