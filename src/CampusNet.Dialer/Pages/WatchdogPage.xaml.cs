using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CampusNet.Core.Configuration;
using CampusNet.Core.Models;
using CampusNet.Core.Startup;
using Wpf.Ui.Controls;

namespace CampusNet.Dialer.Pages;

public partial class WatchdogPage : Page
{
    private AppSettings _settings = new();
    private bool _loading = true;
    private readonly DispatcherTimer _timer;

    public WatchdogPage()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) => RefreshRuntime();
        Loaded += OnLoaded;
        Unloaded += (_, _) => _timer.Stop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _settings = SettingsStore.Load();
        _loading = true;

        IntervalSlider.Value = _settings.CheckIntervalSeconds;
        ThresholdSlider.Value = _settings.FailThreshold;
        CooldownSlider.Value = _settings.CooldownSeconds;
        AutoStartSwitch.IsChecked = SafeAutoStartEnabled();
        WatchdogSwitch.IsChecked = SafeWatchdogRunning();

        _loading = false;
        UpdateLabels();
        RefreshRuntime();
        _timer.Start();
    }

    private void OnWatchdogChecked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        try
        {
            WatchdogController.Start();
            Show("守护已启动，断线将自动重拨。", InfoBarSeverity.Success);
            _timer.Start();
            RefreshRuntime();
        }
        catch (Exception ex)
        {
            Show("启动失败：" + ex.Message, InfoBarSeverity.Error);
            WatchdogSwitch.IsChecked = false;
        }
    }

    private void OnWatchdogUnchecked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        try
        {
            WatchdogController.Stop();
            Show("守护已停止。", InfoBarSeverity.Informational);
            RefreshRuntime();
        }
        catch (Exception ex)
        {
            Show("停止失败：" + ex.Message, InfoBarSeverity.Error);
        }
    }

    private void OnAutoStartChecked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        try
        {
            AutoStartManager.Enable();
            _settings.AutoStart = true;
            SettingsStore.Save(_settings);
            Show("已设置开机自启。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Show("设置失败：" + ex.Message, InfoBarSeverity.Error);
        }
    }

    private void OnAutoStartUnchecked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        try
        {
            AutoStartManager.Disable();
            _settings.AutoStart = false;
            SettingsStore.Save(_settings);
            Show("已取消开机自启。", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            Show("操作失败：" + ex.Message, InfoBarSeverity.Error);
        }
    }

    private void OnIntervalChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        IntervalValue.Text = $"{(int)IntervalSlider.Value} 秒";
        Persist();
    }

    private void OnThresholdChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        ThresholdValue.Text = $"{(int)ThresholdSlider.Value} 次";
        Persist();
    }

    private void OnCooldownChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        CooldownValue.Text = $"{(int)CooldownSlider.Value} 秒";
        Persist();
    }

    private void Persist()
    {
        if (_loading) return;
        _settings.CheckIntervalSeconds = (int)IntervalSlider.Value;
        _settings.FailThreshold = (int)ThresholdSlider.Value;
        _settings.CooldownSeconds = (int)CooldownSlider.Value;
        SettingsStore.Save(_settings);
    }

    private void UpdateLabels()
    {
        IntervalValue.Text = $"{_settings.CheckIntervalSeconds} 秒";
        ThresholdValue.Text = $"{_settings.FailThreshold} 次";
        CooldownValue.Text = $"{_settings.CooldownSeconds} 秒";
    }

    private void RefreshRuntime()
    {
        bool running = SafeWatchdogRunning();
        // 同步开关状态（外部停止时反映到 UI）
        if (!_loading && WatchdogSwitch.IsChecked != running)
        {
            _loading = true;
            WatchdogSwitch.IsChecked = running;
            _loading = false;
        }

        if (!running)
        {
            StateRunning.Text = "已停止";
            StatePid.Text = "—";
            StateStartTime.Text = "—";
            StateLastCheck.Text = "—";
            StateLastDial.Text = "—";
            return;
        }

        StateRunning.Text = "运行中";
        var st = WatchdogState.Load();
        if (st == null) return;

        StatePid.Text = st.Pid.ToString();
        StateStartTime.Text = st.StartTime.ToString("MM-dd HH:mm:ss");
        StateLastCheck.Text = st.LastCheckTime.ToString("HH:mm:ss");
        StateLastDial.Text = st.LastDialTime?.ToString("MM-dd HH:mm:ss") ?? "尚未重拨";
    }

    private static bool SafeWatchdogRunning()
    {
        try { return WatchdogController.IsRunning(); } catch { return false; }
    }

    private static bool SafeAutoStartEnabled()
    {
        try { return AutoStartManager.IsEnabled(); } catch { return false; }
    }

    private void Show(string text, InfoBarSeverity severity)
    {
        MessageBar.Message = text;
        MessageBar.Severity = severity;
        MessageBar.IsOpen = true;
    }
}
