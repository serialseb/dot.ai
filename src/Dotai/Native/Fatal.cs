using Dotai.Services;
using Dotai.Ui;

namespace Dotai.Native;

public static class Fatal
{
    public static void Die(NativeStringView msg)
    {
        ConsoleOut.Error(msg);
        Libc.Abort();
    }
}
