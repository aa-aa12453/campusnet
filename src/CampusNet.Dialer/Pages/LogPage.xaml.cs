using System.IO;
using System.Windows;
using System.Windows.Controls;
using CampusNet.Core.Logging;
using Microsoft.Win32;

namespace CampusNet.Dialer.Pages;

public partial class LogPage : Page
{
    private AppLogger _dialLog = new(LogKind.Dial);
    private AppLogger _watchdogLog = new(LogKind.Watchdog);
    private bool _loading;

    public LogPage()
    {
        InitializeComponent();
        Loaded += (_, _) => ReloadMonths(showCurrent: true);
    }

    private LogKind CurrentKind =>
        KindCombo.SelectedIndex == 1 ? LogKind.Watchdog : LogKind.Dial;

    private AppLogger CurrentLogger =>
        CurrentKind == LogKind.Watchdog ? _watchdogLog : _dialLog;

    private void ReloadMonths(bool showCurrent)
    {
        _loading = true;
        var months = CurrentLogger.ListMonths();
        if (months.Count == 0)
        {
            months.Add(new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1));
        }

        MonthCombo.Items.Clear();
        foreach (var m in months)
            MonthCombo.Items.Add(new ComboBoxItem { Content = m.ToString("yyyy-MM"), Tag = m });

        MonthCombo.SelectedIndex = 0;
        _loading = false;
        ShowSelectedMonth();
    }

    private void ShowSelectedMonth()
    {
        DateTime? when = null;
        if (MonthCombo.SelectedItem is ComboBoxItem item && item.Tag is DateTime m)
            when = m;

        string text = CurrentLogger.Read(when);
        LogBox.Text = string.IsNullOrEmpty(text)
            ? "（该月暂无日志）"
            : text;
        LogBox.ScrollToEnd();
    }

    private void OnKindChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ReloadMonths(showCurrent: true);
    }

    private void OnMonthChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        ShowSelectedMonth();
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        ReloadMonths(showCurrent: true);
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            FileName = (CurrentKind == LogKind.Watchdog ? "watchdog-" : "dial-") + DateTime.Now.ToString("yyyy-MM"),
            DefaultExt = ".log",
            Filter = "日志文件 (*.log)|*.log|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            File.WriteAllText(dlg.FileName, LogBox.Text);
        }
    }
}
