namespace DuoTuXiuXiu.Services;

/// <summary>把未处理异常写入本地日志，便于用户反馈问题时定位。</summary>
public static class CrashLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DuoTuXiuXiu", "crash.log");

    public static string FilePath => LogPath;

    public static void Write(Exception? exception, string? note = null)
    {
        if (exception is null && string.IsNullOrEmpty(note)) return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {note}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // 写日志失败时不再向上抛出，避免二次崩溃
        }
    }
}
