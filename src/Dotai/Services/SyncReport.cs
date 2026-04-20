namespace Dotai.Services;

public sealed class SyncReport
{
    public List<string> ManualRepos { get; } = new();
    public List<string> Conflicts { get; } = new();
    public int SkillsLinked { get; set; }
    public int FilesLinked { get; set; }

    public bool Ok => ManualRepos.Count == 0 && Conflicts.Count == 0;
}
