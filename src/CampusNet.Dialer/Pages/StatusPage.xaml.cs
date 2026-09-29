using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CampusNet.Core.Credentials;
using CampusNet.Core.Models;
using CampusNet.Core.Network;
using CampusNet.Core.Ras;
using CampusNet.Core.Startup;
using Wpf.Ui.Controls;

namespace CampusNet.Dialer.Pages;

public partial class StatusPage : Page
{
    private readonly DialService _dialer = new();
    private readonly DispatcherTimer _timer;

    private static readonly SolidColorBrush Green = new(Color.FromRgb(0x2E, 0xA6, 0x4F));
    private static readonly SolidColorBrush Gray = new(Color.FromRgb(0x8A, 0x8A, 0x8A));
    private static readonly SolidColorBrush Red = new(Color.FromRgb(0xC3, 0x00, 0x17));

    public StatusPage()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { Refresh(); _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private void Refresh()
    {
        var cred = CredentialStore.Load();
        bool watchdogRunning = SafeWatchdog();

        if (cred == null || string.IsNullOrEmpty(cred.ConnectionName))
        {
            StatusDot.Fill = Gray;
            StatusText.Text = "尚未设置账号";
            ConnectionNameText.Text = "—";
            IpAddressText.Text = "—";
            DialButton.IsEnabled = false;
            HangUpButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            DialButton.IsEnabled = true;
            ConnectionNameText.Text = cred.ConnectionName;
            bool connected = _dialer.IsConnected(cred.ConnectionName);

            if (connected)
            {
                StatusDot.Fill = Green;
                StatusText.Text = "已连接";
                IpAddressText.Text = NetworkMonitor.GetRasIpAddress(cred.ConnectionName) ?? "—";
                HangUpButton.Visibility = Visibility.Visible;
                DialButton.Content = "重新拨号";
            }
            else
            {
                StatusDot.Fill = Gray;
                StatusText.Text = "未连接";
                IpAddressText.Text = "—";
                HangUpButton.Visibility = Visibility.Collapsed;
                DialButton.Content = "立即拨号";
            }
        }

        // 守护状态
        if (watchdogRunning)
        {
            WatchdogDot.Fill = Green;
            WatchdogText.Text = "自动重拨：运行中";
            var st = WatchdogState.Load();
            WatchdogDetail.Text = st != null
                ? $"上次检测 {st.LastCheckTime:HH:mm:ss}，断线将自动重连。"
                : "断线将自动重连。";
        }
        else
        {
            WatchdogDot.Fill = Gray;
            WatchdogText.Text = "自动重拨：已停止";
            WatchdogDetail.Text = "建议开启，息屏/休眠恢复后自动联网。";
        }
    }

    private static bool SafeWatchdog()
    {
        try { return WatchdogController.IsRunning(); } catch { return false; }
    }

    private async void OnDialClick(object sender, RoutedEventArgs e)
    {
        var cred = CredentialStore.Load();
        if (cred == null)
        {
            ShowMessage("请先在「账号设置」里填写账号和密码。", InfoBarSeverity.Warning);
            return;
        }

        SetBusy(true);
        ShowMessage("正在拨号，请稍候…", InfoBarSeverity.Informational, false);
        try
        {
            var result = await Task.Run(async () => await _dialer.DialOnceAsync(cred));
            if (result.Success)
                ShowMessage($"拨号成功，IP：{result.IpAddress}", InfoBarSeverity.Success);
            else
                ShowMessage(DescribeError(result.ErrorCode), InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            ShowMessage("拨号异常：" + ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            SetBusy(false);
            Refresh();
        }
    }

    private void OnHangUpClick(object sender, RoutedEventArgs e)
    {
        var cred = CredentialStore.Load();
        if (cred == null) return;
        _dialer.HangUp(cred.ConnectionName);
        ShowMessage("已挂断连接。", InfoBarSeverity.Informational);
        Refresh();
    }

    private void OnGoWatchdog(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow w)
            w.RootNavigation.Navigate(typeof(WatchdogPage));
    }

    private void SetBusy(bool busy)
    {
        BusyRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        DialButton.IsEnabled = !busy;
        HangUpButton.IsEnabled = !busy;
    }

    private void ShowMessage(string text, InfoBarSeverity severity, bool open = true)
    {
        MessageBar.Message = text;
        MessageBar.Severity = severity;
        MessageBar.IsOpen = open;
    }

    /// <summary>把常见错误码转成同学能看懂的话。</summary>
    public static string DescribeError(uint code) => code switch
    {
        691 => "账号或密码不正确（错误码 691）。请检查密码是否过期，或账号是否在别处登录。",
        623 => "找不到拨号连接（错误码 623）。请到「账号设置」重新选择或创建连接。",
        678 => "远程计算机无响应（错误码 678）。多半是网线、交换机或端口问题。",
        812 => "被服务器策略阻止（错误码 812），请联系网络中心。",
        633 => "拨号端口被占用（错误码 633），稍等片刻再试。",
        _ => $"拨号失败（错误码 {code}）。"
    };
}
