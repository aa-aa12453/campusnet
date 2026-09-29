using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace CampusNet.Core.Startup;

/// <summary>
/// 守护进程开机自启管理。双保险：
/// 1) HKCU Run 键——登录即启动主程序 exe（带守护参数）；
/// 2) 计划任务——每 5 分钟兜底拉起（守护单实例，重复启动自动退出）。
/// 均为当前用户范围，不需要管理员权限。
/// </summary>
public static class AutoStartManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CampusNetWatchdog";
    private const string TaskName = "CampusNetWatchdog";

    /// <summary>主程序 exe 路径（守护与 GUI 是同一个 exe）。</summary>
    public static string AppExePath =>
        Environment.ProcessPath
        ?? Path.Combine(AppContext.BaseDirectory, "校园网拨号.exe");

    public static string WatchdogExePath => AppExePath;

    public static bool WatchdogExists() => File.Exists(AppExePath);

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void Enable()
    {
        string exe = AppExePath;
        string workDir = Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory;

        // 1) Run 键：登录后以守护模式启动。
        //    必须先 cd 到 exe 所在目录再启动，否则 Windows 会用「桌面」作为
        //    工作目录，导致程序把 watchdog.state / logs 等数据文件写到桌面上。
        using (var key = Registry.CurrentUser.CreateSubKey(RunKey, true))
        {
            key.SetValue(ValueName,
                $"cmd /c cd /d \"{workDir}\" && start \"\" \"{exe}\" {WatchdogController.WatchdogArg}");
        }

        // 2) 兜底计划任务（失败不阻断，Run 键已足够）
        TryRegisterTask(exe, workDir);
    }

    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            key?.DeleteValue(ValueName, false);
        }
        catch { }
        TryUnregisterTask();
    }

    private static void TryRegisterTask(string exe, string workDir)
    {
        try
        {
            string ps =
                "$a = New-ScheduledTaskAction -Execute '\"" + exe + "\"' -Argument '" +
                WatchdogController.WatchdogArg + "' -WorkingDirectory '\"" + workDir + "\"';" +
                "$t = New-ScheduledTaskTrigger -AtLogOn;" +
                "$t.Repetition = (New-ScheduledTaskTrigger -Once -At (Get-Date) " +
                "-RepetitionInterval (New-TimeSpan -Minutes 5)).Repetition;" +
                "$s = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries " +
                "-DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Minutes 2) " +
                "-RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1);" +
                "Register-ScheduledTask -TaskName '" + TaskName + "' -Action $a " +
                "-Trigger $t -Settings $s -Force | Out-Null";
            RunPowerShell(ps);
        }
        catch { }
    }

    private static void TryUnregisterTask()
    {
        try
        {
            RunPowerShell("Unregister-ScheduledTask -TaskName '" + TaskName +
                          "' -Confirm:$false -ErrorAction SilentlyContinue");
        }
        catch { }
    }

    private static void RunPowerShell(string command)
    {
        var psi = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\""
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit(15000);
    }
}
