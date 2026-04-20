using Dotai.Native;

namespace Dotai.Services;

public struct RepoConfig
{
    public NativeString Name;  // "foo/bar"
    public NativeString Mode;  // "merge"

    public void Dispose() { Name.Dispose(); Mode.Dispose(); }
}
