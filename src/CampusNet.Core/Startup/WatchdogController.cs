using System.Diagnostics;
using System.IO;
using System.Threading;
using CampusNet.Core.Models;

namespace CampusNet.Core.Startup;

/// <summary>
/// 启动、停止、检测守护进程。守护与 GUI 共用同一个 exe，通过
/// <see cref="WatchdogArg"/> 参数以后台无窗口模式启动，因此这里不再
/// 按进程名管理（会和 GUI 同名），而改用命名 Mutex / Event 通信。
/// </summary>
public static class WatchdogController
{
    /// <summary>让 exe 进入后台守护模式的命令行参数。</summary>
    public const string WatchdogArg = "--watchdog";

    /// <summary>守护单实例 Mutex，也用于 GUI 判断守护是否存活。</summary>
    public const string MutexName = @"Global\CampusNetWatchdog";

    /// <summary>GUI 设置此事件，通知守护自行退出。</summary>
    public const string StopEventName = @"Global\CampusNetWatchdogStop";

    public static bool IsRunning()
    {
        try
        {
            if (!Mutex.TryOpenExisting(MutexName, out var mutex))
                return false;
            mutex.Dispose();

            // Mutex 在，且状态文件较新（约 3 分钟内有心跳）才算真活着
            var state = WatchdogState.Load();
            if (state == null) return true;
            return (DateTime.Now - state.LastCheckTime).TotalSeconds < 180;
        }
        catch
        {
            return false;
        }
    }

    public static void Start()
    {
        if (IsRunning()) return;

        string exe = CurrentExePath();
        if (!File.Exists(exe)) throw new FileNotFoundException("找不到主程序：" + exe);

        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(exe)
        };
        psi.ArgumentList.Add(WatchdogArg);
        Process.Start(psi);
    }

    public static void Stop()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(StopEventName, out var stopEvent))
            {
                using (stopEvent) stopEvent.Set();
            }
        }
        catch { }
    }

    private static string CurrentExePath()
    {
        return Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "校园网拨号.exe");
    }
}
