using System.Diagnostics;

namespace Dotai.Tests.Fixtures;

public static class LocalGitRepo
{
    public static void Run(string workDir, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["GIT_AUTHOR_NAME"] = "test";
        psi.Environment["GIT_AUTHOR_EMAIL"] = "test@example.com";
        psi.Environment["GIT_COMMITTER_NAME"] = "test";
        psi.Environment["GIT_COMMITTER_EMAIL"] = "test@example.com";
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException(
                $"git {string.Join(' ', args)} failed: {p.StandardError.ReadToEnd()}");
    }

    // Creates a bare remote repo + a populated working clone, returns (remoteUrl, workingClone).
    public static (string remoteUrl, string workingClone) CreateRemoteWithContent(
        string baseDir, Action<string>? populate = null)
    {
        var bare = Path.Combine(baseDir, "remote.git");
        Directory.CreateDirectory(bare);
        Run(bare, "init", "--bare", "--initial-branch=main");

        var work = Path.Combine(baseDir, "work");
        Directory.CreateDirectory(work);
        var remoteUrl = "file://" + bare.Replace('\\', '/');
        Run(baseDir, "clone", remoteUrl, work);

        populate?.Invoke(work);

        Run(work, "add", "-A");
        Run(work, "commit", "-m", "initial");
        Run(work, "push", "origin", "main");
        return (remoteUrl, work);
    }
}
