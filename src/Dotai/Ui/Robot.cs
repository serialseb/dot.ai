using Dotai.Services;

namespace Dotai.Ui;

public static class Robot
{
    private static ReadOnlySpan<byte> Art => """
           [■_■]
          /|___|\
         /_|   |_\
         |  📖  |
         |______|
          /    \
         /      \
        /________\

        """u8;

    private static ReadOnlySpan<byte> ClearScreen => "\x1b[2J\x1b[H"u8;

    public static void ShowIfTty()
    {
        if (!Stdio.IsTty(1)) return;
        Stdio.Write(1, Art);
        // nanosleep for 1 second (tv_sec=1, tv_nsec=0) via a 16-byte timespec on stack
        Span<byte> ts = stackalloc byte[16];
        BitConverter.TryWriteBytes(ts[..8], 1L);
        BitConverter.TryWriteBytes(ts.Slice(8, 8), 0L);
        unsafe { fixed (byte* p = ts) Libc.Nanosleep(p, null); }
        Stdio.Write(1, ClearScreen);
    }
}
