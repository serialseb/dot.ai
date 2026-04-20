using System.Runtime.InteropServices;

namespace Dotai.Native;

public unsafe struct NativeList<T> where T : unmanaged
{
    private T* _ptr;
    private int _len;
    private int _cap;

    public NativeList(int initialCapacity)
    {
        _cap = initialCapacity < 4 ? 4 : initialCapacity;
        _ptr = (T*)NativeMemory.Alloc((nuint)(_cap * sizeof(T)));
        _len = 0;
    }

    public int Length => _len;
    public ref T this[int i] => ref _ptr[i];
    public NativeListView<T> AsView() => new(_ptr, _len);

    public void Add(T item)
    {
        EnsureCapacity(_len + 1);
        _ptr[_len++] = item;
    }

    private void EnsureCapacity(int need)
    {
        if (need <= _cap) return;
        int newCap = _cap * 2;
        while (newCap < need) newCap *= 2;
        T* np = (T*)NativeMemory.Alloc((nuint)(newCap * sizeof(T)));
        if (_len > 0) new ReadOnlySpan<T>(_ptr, _len).CopyTo(new Span<T>(np, _len));
        NativeMemory.Free(_ptr);
        _ptr = np;
        _cap = newCap;
    }

    public void Dispose()
    {
        if (_ptr == null) return;
        NativeMemory.Free(_ptr);
        _ptr = null;
        _len = 0;
        _cap = 0;
    }
}
