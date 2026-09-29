using CampusNet.Core.Configuration;
using CampusNet.Core.Credentials;
using CampusNet.Core.Logging;
using CampusNet.Core.Models;
using CampusNet.Core.Network;
using CampusNet.Core.Ras;
using CampusNet.Core.Startup;

namespace CampusNet.Dialer;

/// <summary>
/// 断线守护的实际执行体。与 GUI 共用同一个 exe：当 exe 以
/// <see cref="WatchdogController.WatchdogArg"/> 参数启动时，App 不创建窗口，
/// 而是在后台线程运行本类。
/// </summary>
public static class WatchdogRunner
{
    public static void Run()
    {
        // 单实例：计划任务每 5 分钟兜底拉起时，重复实例直接退出
        using var single = new Mutex(true, WatchdogController.MutexName, out bool created);
        if (!created) return;

        // GUI 通过设置这个事件通知守护退出
        using var stopEvent = new EventWaitHandle(
            false, EventResetMode.ManualReset, WatchdogController.StopEventName);

        AppPaths.EnsureDirs();
        var log = new AppLogger(LogKind.Watchdog);
        var dialer = new DialService();
        var settings = SettingsStore.Load();

        var state = new WatchdogState
        {
            Pid = Environment.ProcessId,
            StartTime = DateTime.Now,
            LastCheckTime = DateTime.Now
        };
        state.Save();

        log.Info($"守护进程启动，PID={state.Pid}，检测间隔={settings.CheckIntervalSeconds}秒，" +
                 $"失败阈值={settings.FailThreshold}，冷却={settings.CooldownSeconds}秒");

        int consecutiveFails = 0;
        DateTime nextAllowedDial = DateTime.MinValue;

        // 启动后给系统网络组件一点就绪时间
        if (stopEvent.WaitOne(TimeSpan.FromSeconds(Math.Min(20, settings.CheckIntervalSeconds))))
        {
            log.Info("启动等待期间收到停止信号，守护退出。");
            return;
        }

        while (true)
        {
            bool healthy;
            try { healthy = NetworkMonitor.HasInternetAsync().GetAwaiter().GetResult(); }
            catch { healthy = false; }

            state.LastCheckTime = DateTime.Now;
            state.NetworkHealthy = healthy;

            if (healthy)
            {
                if (consecutiveFails > 0) log.Info("网络已恢复。");
                consecutiveFails = 0;
                state.ConsecutiveFails = 0;
            }
            else
            {
                consecutiveFails++;
                state.ConsecutiveFails = consecutiveFails;
                log.Warn($"网络不可达，连续第 {consecutiveFails} 次。");

                if (consecutiveFails >= settings.FailThreshold && DateTime.Now >= nextAllowedDial)
                {
                    var cred = CredentialStore.Load();
                    if (cred == null || string.IsNullOrEmpty(cred.ConnectionName))
                    {
                        log.Error("未找到拨号凭据，无法自动重拨。请在程序中设置账号。");
                    }
                    else
                    {
                        log.Info($"开始自动重拨，连接={cred.ConnectionName}，账号={DialService.Mask(cred.UserName)}");
                        state.LastDialTime = DateTime.Now;

                        var result = dialer.DialAsync(cred, settings).GetAwaiter().GetResult();
                        state.LastDialCode = result.Success ? 0u : result.ErrorCode;

                        if (result.Success)
                        {
                            log.Info($"自动重拨成功，IP={result.IpAddress}");
                            consecutiveFails = 0;
                            state.ConsecutiveFails = 0;
                            state.NetworkHealthy = true;
                        }
                        else
                        {
                            log.Error($"自动重拨失败，错误码={result.ErrorCode}：{result.Message}");
                        }
                    }

                    // 无论成功失败都进入冷却，避免反复折腾网卡
                    nextAllowedDial = DateTime.Now.AddSeconds(settings.CooldownSeconds);
                    log.Info($"进入 {settings.CooldownSeconds} 秒冷却。");
                }
            }

            state.Save();

            // 每轮重新读取设置，GUI 改了间隔能即时生效；同时检查停止信号
            settings = SettingsStore.Load();
            if (stopEvent.WaitOne(TimeSpan.FromSeconds(settings.CheckIntervalSeconds)))
            {
                log.Info("收到停止信号，守护退出。");
                return;
            }
        }
    }
}
