using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CampusNet.Core.Configuration;
using CampusNet.Core.Credentials;
using CampusNet.Core.Startup;

namespace CampusNet.Dialer.Pages;

public partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
            DataLocationText.Text = "数据位置：" + AppPaths.BaseDir;
    }

    private void OnOpenData(object sender, RoutedEventArgs e)
    {
        try
        {
            AppPaths.EnsureDirs();
            Process.Start(new ProcessStartInfo("explorer.exe",
                "\"" + AppPaths.BaseDir + "\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("打开失败：" + ex.Message);
        }
    }

    private void OnUninstall(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "确定要彻底卸载吗？\n\n将停止守护、取消开机自启、删除账号密码和程序文件，此操作不可恢复。",
            "确认卸载", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            WatchdogController.Stop();
            AutoStartManager.Disable();
            CredentialStore.Clear();
        }
        catch { }

        string programDir = AppContext.BaseDirectory.TrimEnd('\\');
        string dataDir = AppPaths.BaseDir.TrimEnd('\\');

        // 启动隐藏的延迟删除脚本，等程序退出后删除程序目录与数据目录
        string bat = Path.Combine(Path.GetTempPath(), "campusnet-uninstall-" + Guid.NewGuid().ToString("N") + ".bat");
        string script =
            "@echo off\r\n" +
            ":retry\r\n" +
            "timeout /t 1 /nobreak >nul\r\n" +
            $"rd /s /q \"{programDir}\"\r\n" +
            (string.Equals(programDir, dataDir, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $"rd /s /q \"{dataDir}\"\r\n") +
            $"if exist \"{programDir}\" goto retry\r\n" +
            $"if exist \"{dataDir}\" goto retry\r\n" +
            "del /f /q \"%~f0\" >nul 2>&1\r\n";
        File.WriteAllText(bat, script);

        Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        Application.Current.Shutdown();
    }
}
