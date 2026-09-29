using System.Threading;
using System.Windows;
using System.Windows.Media;
using CampusNet.Core.Startup;
using Wpf.Ui.Appearance;

namespace CampusNet.Dialer;

public partial class App : Application
{
    public static readonly Color BrandRed = Color.FromRgb(0xC3, 0x00, 0x17);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (HasWatchdogArg(e.Args))
        {
            RunAsWatchdog();
            return;
        }

        RunAsGui();
    }

    private static bool HasWatchdogArg(string[] args)
    {
        foreach (var a in args)
            if (string.Equals(a, WatchdogController.WatchdogArg, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>后台守护模式：不创建任何窗口，在专用线程运行守护循环。</summary>
    private void RunAsWatchdog()
    {
        // 没有窗口，必须改为显式关闭，否则 Application 会立即退出
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var thread = new Thread(() =>
        {
            try
            {
                WatchdogRunner.Run();
            }
            finally
            {
                Dispatcher.BeginInvoke(new Action(Shutdown));
            }
        });
        thread.IsBackground = true;
        thread.Start();
    }

    /// <summary>正常图形界面模式。</summary>
    private void RunAsGui()
    {
        ApplicationThemeManager.Apply(ApplicationTheme.Light);
        ApplicationAccentColorManager.Apply(BrandRed, ApplicationTheme.Light);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
