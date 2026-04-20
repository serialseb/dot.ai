using Dotai.Services;

namespace Dotai.Ui;

internal static unsafe class Stdio
{
    public static void Write(int fd, ReadOnlySpan<byte> data)
    {
        fixed (byte* p = data)
        {
            var remaining = (nuint)data.Length;
            var ptr = p;
            while (remaining > 0)
            {
                var written = Libc.Write(fd, ptr, remaining);
                if (written < 0) break; // error; abort
                ptr += written;
                remaining -= (nuint)written;
            }
        }
    }

    public static bool IsTty(int fd) => Libc.IsAtty(fd) == 1;
}
