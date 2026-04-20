namespace Dotai.Services;

public sealed class SyncReport
{
    public List<byte[]> ManualRepos { get; } = new();
    public List<byte[]> Conflicts { get; } = new();
    public int SkillsLinked { get; set; }
    public int FilesLinked { get; set; }

    public bool Ok => ManualRepos.Count == 0 && Conflicts.Count == 0;
}
