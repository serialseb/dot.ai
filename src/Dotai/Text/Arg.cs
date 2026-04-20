namespace Dotai.Text;

public readonly struct Arg
{
    public byte[] Data { get; }
    public Arg(byte[] data) { Data = data; }
    public FastString AsFast => new(Data);
    public static implicit operator Arg(byte[] data) => new(data);
    public static implicit operator ReadOnlySpan<byte>(Arg a) => a.Data;
}
