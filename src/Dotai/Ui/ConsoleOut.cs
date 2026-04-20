namespace Dotai.Ui;

public static class ConsoleOut
{
    public static void Info(string msg) => Console.Out.WriteLine(msg);

    public static void Success(string msg) => Console.Out.WriteLine($"✨ {msg}");

    public static void Hint(string msg) => Console.Out.WriteLine($"ℹ️  {msg}");

    public static void Warn(string msg) => Console.Error.WriteLine($"⚠️  warn: {msg}");

    public static void Error(string msg) => Console.Error.WriteLine($"❌ error: {msg}");

    public static void Detail(string msg) => Console.Error.WriteLine(msg);
}
