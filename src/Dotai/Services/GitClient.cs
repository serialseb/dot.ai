using System.Diagnostics;

namespace Dotai.Services;

public record GitResult(int ExitCode, string StdOut, string StdErr);

public static class GitClient
{
    public static GitResult Run(string workDir, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();
        p.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);
        return new GitResult(p.ExitCode, stdoutTask.Result, stderrTask.Result);
    }

    public static GitResult Clone(string url, string target)
    {
        var parent = Path.GetDirectoryName(target) ?? ".";
        Directory.CreateDirectory(parent);
        return Run(parent, "clone", url, target);
    }

    public static GitResult StatusPorcelain(string workDir) =>
        Run(workDir, "status", "--porcelain");

    public static GitResult AddAll(string workDir) =>
        Run(workDir, "add", "-A");

    public static GitResult Commit(string workDir, string message) =>
        Run(workDir, "commit", "-m", message);

    public static GitResult Fetch(string workDir) =>
        Run(workDir, "fetch", "origin");

    public static GitResult Rebase(string workDir, string upstream) =>
        Run(workDir, "rebase", upstream);

    public static GitResult Push(string workDir, string branch) =>
        Run(workDir, "push", "origin", branch);

    public static string DefaultBranch(string workDir)
    {
        var r = Run(workDir, "symbolic-ref", "refs/remotes/origin/HEAD");
        if (r.ExitCode != 0) return "main";
        var line = r.StdOut.Trim();
        var slash = line.LastIndexOf('/');
        return slash < 0 ? "main" : line[(slash + 1)..];
    }

    public static bool RebaseInProgress(string workDir) =>
        Directory.Exists(Path.Combine(workDir, ".git", "rebase-merge"))
        || Directory.Exists(Path.Combine(workDir, ".git", "rebase-apply"));
}
