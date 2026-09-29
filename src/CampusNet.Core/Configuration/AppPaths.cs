using System.IO;

namespace CampusNet.Core.Configuration;

/// <summary>
/// 统一的数据路径。绿色便携：优先用 exe 所在目录；不可写时回退到 LOCALAPPDATA。
/// GUI 与守护 exe 同目录时自动共享凭据、设置和日志。
/// </summary>
public static class AppPaths
{
    private static readonly string AppDataFallback =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CampusNetDialer");

    public static string BaseDir { get; } = ResolveBaseDir();

    public static string CredentialFile => Path.Combine(BaseDir, "credential.bin");
    public static string SettingsFile => Path.Combine(BaseDir, "settings.json");
    public static string WatchdogPidFile => Path.Combine(BaseDir, "watchdog.pid");
    public static string WatchdogStateFile => Path.Combine(BaseDir, "watchdog.state");
    public static string TempPbk => Path.Combine(BaseDir, "temp-dial.pbk");
    public static string LogDir => Path.Combine(BaseDir, "logs");

    private static string ResolveBaseDir()
    {
        // 注意：这里必须用 AppContext.BaseDirectory（exe 所在目录），
        // 不能用 Environment.CurrentDirectory —— 后者会受启动方影响。
        // 若从注册表 Run 项启动且未指定工作目录，CurrentDirectory 会是「桌面」，
        // 曾导致 watchdog.state / logs 被错误写到桌面上。
        string exeDir = AppContext.BaseDirectory;
        try
        {
            string probe = Path.Combine(exeDir, ".write-test");
            File.WriteAllText(probe, "1");
            File.Delete(probe);
            return exeDir.TrimEnd(Path.DirectorySeparatorChar);
        }
        catch
        {
            Directory.CreateDirectory(AppDataFallback);
            return AppDataFallback;
        }
    }

    public static void EnsureDirs()
    {
        Directory.CreateDirectory(BaseDir);
        Directory.CreateDirectory(LogDir);
    }

    public static string DialLogFile(DateTime? when = null)
        => Path.Combine(LogDir, $"dial-{when:yyyy-MM}.log");

    public static string WatchdogLogFile(DateTime? when = null)
        => Path.Combine(LogDir, $"watchdog-{when:yyyy-MM}.log");
}
