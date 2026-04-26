using System.Text;
using Dotai.Commands;
using Dotai.Native;
using Dotai.Services;
using Dotai.Tests.Fixtures;
using Xunit;

namespace Dotai.Tests;

public class StatusCommandTests
{
    private static NativeStringView V(string s) => Encoding.UTF8.GetBytes(s);

    private static NativeList<NativeString> EmptyArgs() => new NativeList<NativeString>(1);

    [Fact]
    public void RejectsWhenNotInsideGitRepo()
    {
        using var tmp = new TempDir();
        var cmd = new StatusCommand(V(tmp.Path));
        var args = EmptyArgs();

        var code = cmd.Execute(args.AsView());
        args.Dispose();

        Assert.Equal(1, code);
    }

    [Fact]
    public void HintsWhenNoRepositoriesConfigured()
    {
        using var tmp = new TempDir();
        var repo = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(repo);
        LocalGitRepo.Run(repo, "init", "--initial-branch=main");
        Directory.CreateDirectory(Path.Combine(repo, ".ai"));
        File.WriteAllText(Path.Combine(repo, ".ai", "config.toml"), "# empty\n");

        var cmd = new StatusCommand(V(repo));
        var args = EmptyArgs();

        var code = cmd.Execute(args.AsView());
        args.Dispose();

        Assert.Equal(0, code);
    }

    [Fact]
    public void ListsSkillsAndFilesForConfiguredRepos()
    {
        using var tmp = new TempDir();
        var (remoteUrl, _) = LocalGitRepo.CreateRemoteWithContent(tmp.Path, w =>
        {
            Directory.CreateDirectory(Path.Combine(w, "skills", "alpha"));
            File.WriteAllText(Path.Combine(w, "skills", "alpha", "SKILL.md"), "x");
            Directory.CreateDirectory(Path.Combine(w, "skills", "beta"));
            File.WriteAllText(Path.Combine(w, "skills", "beta", "SKILL.md"), "y");
            Directory.CreateDirectory(Path.Combine(w, "files"));
            File.WriteAllText(Path.Combine(w, "files", "AGENTS.shared.md"), "shared");
        });
        var repo = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(repo);
        LocalGitRepo.Run(repo, "init", "--initial-branch=main");
        var aiDir = Path.Combine(repo, ".ai");
        Directory.CreateDirectory(Path.Combine(aiDir, "repositories"));

        var cloneName = "github.com▸owner▸repo";
        GitClient.Clone(V(remoteUrl), V(Path.Combine(aiDir, "repositories", cloneName))).Dispose();

        var config = new NativeList<RepoConfig>(1);
        ConfigStore.AddRepo(ref config, V("owner/repo"), "merge"u8);
        ConfigStore.Save(V(Path.Combine(aiDir, "config.toml")), config.AsView());
        for (int i = 0; i < config.Length; i++) config[i].Dispose();
        config.Dispose();

        var cmd = new StatusCommand(V(repo));
        var args = EmptyArgs();
        var code = cmd.Execute(args.AsView());
        args.Dispose();

        Assert.Equal(0, code);
    }
}
