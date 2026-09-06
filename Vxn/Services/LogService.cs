using System.Collections.ObjectModel;

namespace Vxn.Services;

public record LogLine(DateTime Time, string Type, string Payload);

/// <summary>
/// 日志：应用日志写文件；内核 stdout 环形缓冲 + 写文件，供日志页展示启动错误。
/// </summary>
public static class LogService
{
    private const int CoreBufferSize = 2000;
    private static readonly object Lock = new();
    private static readonly Queue<LogLine> CoreBuffer = new();

    /// <summary>内核 stdout/stderr 日志（含启动错误）。</summary>
    public static ObservableCollection<LogLine> CoreLogs { get; } = new();

    private static DateTime _lastCoreLogWriteTime = DateTime.MinValue;

    public static void Core(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        var entry = new LogLine(DateTime.Now, "core", line.TrimEnd());
        lock (Lock)
        {
            CoreBuffer.Enqueue(entry);
            if (CoreBuffer.Count > CoreBufferSize) CoreBuffer.Dequeue();
        }
        AppendToFile(Paths.CoreLogFile, $"[{entry.Time:HH:mm:ss}] {entry.Payload}");
    }

    public static List<LogLine> GetCoreLogs()
    {
        lock (Lock) return CoreBuffer.ToList();
    }

    public static void App(string message, string level = "info")
    {
        var entry = new LogLine(DateTime.Now, level, message);
        AppendToFile(Paths.AppLogFile, $"[{entry.Time:yyyy-MM-dd HH:mm:ss}] [{level}] {message}");
    }

    private static void AppendToFile(string file, string line)
    {
        try
        {
            lock (Lock)
            {
                // 简易按日滚动
                var dir = Path.GetDirectoryName(file)!;
                var dayFile = Path.Combine(dir, Path.GetFileNameWithoutExtension(file) + $"-{DateTime.Now:yyyy-MM-dd}" + Path.GetExtension(file));
                File.AppendAllText(dayFile, line + Environment.NewLine);
                File.AppendAllText(file, line + Environment.NewLine);
                _lastCoreLogWriteTime = DateTime.Now;
            }
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }

    /// <summary>清理 N 天前的旧日志。</summary>
    public static void CleanupOldLogs(int days)
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(Paths.LogsDir))
            {
                if (f.EndsWith("app.log") || f.EndsWith("core.log")) continue;
                if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-days)) File.Delete(f);
            }
        }
        catch { }
    }
}
