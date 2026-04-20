namespace Dotai.Native;

public readonly unsafe ref struct NativeListView<T> where T : unmanaged
{
    private readonly T* _ptr;
    private readonly int _len;
    internal NativeListView(T* ptr, int len) { _ptr = ptr; _len = len; }
    public int Length => _len;
    public ref readonly T this[int i] => ref _ptr[i];
    public NativeListView<T> Slice(int start)
        => new(_ptr + start, _len - start);
}
