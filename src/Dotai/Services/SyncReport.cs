using Dotai.Native;

namespace Dotai.Services;

public struct SyncReport
{
    public NativeList<NativeString> ManualRepos;
    public NativeList<NativeString> Conflicts;
    public int SkillsNew;
    public int SkillsUpdated;
    public int SkillsUnchanged;
    public int SkillsGone;
    public int FilesNew;
    public int FilesUpdated;
    public int FilesUnchanged;
    public int FilesGone;

    public SyncReport(int capacity = 4)
    {
        ManualRepos = new NativeList<NativeString>(capacity);
        Conflicts = new NativeList<NativeString>(capacity);
        SkillsNew = SkillsUpdated = SkillsUnchanged = SkillsGone = 0;
        FilesNew = FilesUpdated = FilesUnchanged = FilesGone = 0;
    }

    public int SkillsLinked => SkillsNew + SkillsUpdated + SkillsUnchanged;
    public int FilesLinked => FilesNew + FilesUpdated + FilesUnchanged;

    public bool Ok => ManualRepos.Length == 0 && Conflicts.Length == 0;

    public void Dispose()
    {
        for (int i = 0; i < ManualRepos.Length; i++) ManualRepos[i].Dispose();
        ManualRepos.Dispose();
        for (int i = 0; i < Conflicts.Length; i++) Conflicts[i].Dispose();
        Conflicts.Dispose();
    }
}
