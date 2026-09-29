using System.Windows;
using CampusNet.Core.Credentials;
using CampusNet.Core.Models;
using CampusNet.Core.Ras;
using CampusNet.Core.Startup;
using Wpf.Ui.Controls;

namespace CampusNet.Dialer;

public partial class FirstRunWizard : FluentWindow
{
    private int _step;
    private readonly DialService _dialer = new();

    private static readonly string[] Titles =
        { "欢迎使用", "选择拨号连接", "输入账号密码", "完成设置" };
    private static readonly string[] Subtitles =
    {
        "三步完成校园网自动拨号设置。",
        "选择已有的拨号连接，或新建一个。",
        "凭据会加密保存在本机。",
        "选择推荐选项，然后完成。"
    };

    public FirstRunWizard()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadConnections();
    }

    private void LoadConnections()
    {
        try
        {
            var entries = PhonebookManager.ListEntries();
            ConnectionCombo.ItemsSource = entries;
            if (ConnectionCombo.SelectedIndex < 0 && entries.Count > 0)
                ConnectionCombo.SelectedIndex = 0;
        }
        catch { }
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => LoadConnections();

    private void OnCreate(object sender, RoutedEventArgs e)
    {
        string name = NewConnectionBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
        {
            System.Windows.MessageBox.Show("请输入新连接的名称。");
            return;
        }
        try
        {
            PhonebookManager.CreatePppoeEntry(name);
            LoadConnections();
            ConnectionCombo.SelectedItem = name;
            NewConnectionBox.Text = string.Empty;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("创建失败：" + ex.Message);
        }
    }

    private void ShowStep()
    {
        StepTitle.Text = Titles[_step];
        StepSubtitle.Text = Subtitles[_step];

        Panel0.Visibility = _step == 0 ? Visibility.Visible : Visibility.Collapsed;
        Panel1.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Panel2.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Panel3.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;

        BackButton.Visibility = _step == 0 ? Visibility.Collapsed : Visibility.Visible;
        NextButton.Content = _step == 3 ? "完成" : "下一步";
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (_step > 0) { _step--; ShowStep(); }
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (_step == 1 && ConnectionCombo.SelectedItem is not string)
        {
            System.Windows.MessageBox.Show("请选择或创建一个拨号连接。");
            return;
        }
        if (_step == 2)
        {
            if (string.IsNullOrWhiteSpace(UserBox.Text))
            {
                System.Windows.MessageBox.Show("请输入校园网账号。");
                return;
            }
            if (string.IsNullOrEmpty(PassBox.Password))
            {
                System.Windows.MessageBox.Show("请输入密码。");
                return;
            }
        }

        if (_step < 3)
        {
            _step++;
            ShowStep();
            return;
        }

        Finish();
    }

    private void Finish()
    {
        string conn = ConnectionCombo.SelectedItem as string ?? string.Empty;
        var cred = new DialCredential
        {
            ConnectionName = conn,
            UserName = UserBox.Text?.Trim() ?? string.Empty,
            Password = PassBox.Password ?? string.Empty
        };

        try
        {
            CredentialStore.Save(cred);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("保存凭据失败：" + ex.Message);
            return;
        }

        try
        {
            if (EnableAutostart.IsChecked == true)
                AutoStartManager.Enable();
            else
                AutoStartManager.Disable();
        }
        catch { }

        bool test = TestNow.IsChecked == true;
        bool watchdog = EnableWatchdog.IsChecked == true;

        if (watchdog)
        {
            try { WatchdogController.Start(); } catch { }
        }

        DialogResult = true;
        Close();

        if (test)
        {
            _ = Task.Run(async () =>
            {
                var result = await _dialer.DialOnceAsync(cred);
                // 结果已写入拨号日志；失败时不弹窗打扰
                _ = result;
            });
        }
    }
}
