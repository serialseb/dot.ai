using Dotai.Native;

namespace Dotai.Services;

public struct SyncReport
{
    public NativeList<NativeString> ManualRepos;
    public NativeList<NativeString> Conflicts;
    public int SkillsLinked;
    public int FilesLinked;

    public SyncReport(int capacity = 4)
    {
        ManualRepos = new NativeList<NativeString>(capacity);
        Conflicts = new NativeList<NativeString>(capacity);
        SkillsLinked = 0;
        FilesLinked = 0;
    }

    public bool Ok => ManualRepos.Length == 0 && Conflicts.Length == 0;

    public void Dispose()
    {
        for (int i = 0; i < ManualRepos.Length; i++) ManualRepos[i].Dispose();
        ManualRepos.Dispose();
        for (int i = 0; i < Conflicts.Length; i++) Conflicts[i].Dispose();
        Conflicts.Dispose();
    }
}
