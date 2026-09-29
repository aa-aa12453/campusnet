using System.IO;
using CampusNet.Core.Configuration;

namespace CampusNet.Core.Logging;

public enum LogKind
{
    Dial,
    Watchdog
}

/// <summary>按月写日志，线程安全。GUI 可直接读取日志文件展示。</summary>
public sealed class AppLogger
{
    private readonly object _gate = new();
    private readonly LogKind _kind;

    public AppLogger(LogKind kind)
    {
        _kind = kind;
        AppPaths.EnsureDirs();
    }

    private string GetFile() => _kind switch
    {
        LogKind.Watchdog => AppPaths.WatchdogLogFile(DateTime.Now),
        _ => AppPaths.DialLogFile(DateTime.Now)
    };

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    public void Write(string level, string message)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level,-5}] {message}";
        lock (_gate)
        {
            try { File.AppendAllText(GetFile(), line + Environment.NewLine, System.Text.Encoding.UTF8); }
            catch { }
        }
    }

    /// <summary>读取指定月份的日志；月份为 null 时读当前月。</summary>
    public string Read(DateTime? when = null)
    {
        string file = _kind switch
        {
            LogKind.Watchdog => AppPaths.WatchdogLogFile(when ?? DateTime.Now),
            _ => AppPaths.DialLogFile(when ?? DateTime.Now)
        };
        try
        {
            return File.Exists(file) ? File.ReadAllText(file) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>列出有日志的月份。</summary>
    public List<DateTime> ListMonths()
    {
        var result = new List<DateTime>();
        try
        {
            string prefix = _kind == LogKind.Watchdog ? "watchdog-" : "dial-";
            foreach (var f in Directory.GetFiles(AppPaths.LogDir, prefix + "*.log"))
            {
                string name = Path.GetFileNameWithoutExtension(f); // dial-2026-09
                var parts = name.Split('-');
                if (parts.Length == 3 &&
                    int.TryParse(parts[1], out int y) && int.TryParse(parts[2], out int m))
                {
                    result.Add(new DateTime(y, m, 1));
                }
            }
        }
        catch { }
        return result.OrderByDescending(d => d).ToList();
    }
}
