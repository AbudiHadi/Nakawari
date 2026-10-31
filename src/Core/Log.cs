namespace Nakawari.Core;

public static class Log
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex.Message}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {level,-5}  {message}{Environment.NewLine}";
        lock (Gate)
        {
            try { File.AppendAllText(AppPaths.LogFile, line); }
            catch (IOException) { /* logging must never break the app */ }
        }
    }
}
