using System.Windows;
using System.Windows.Controls;
using CampusNet.Core.Credentials;
using CampusNet.Core.Models;
using CampusNet.Core.Ras;
using Wpf.Ui.Controls;

namespace CampusNet.Dialer.Pages;

public partial class AccountPage : Page
{
    private readonly DialService _dialer = new();
    private DialCredential? _original;

    public AccountPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadData();
    }

    private void LoadData()
    {
        LoadConnections();
        _original = CredentialStore.Load();

        if (_original != null)
        {
            UserBox.Text = _original.UserName;
            if (!string.IsNullOrEmpty(_original.ConnectionName))
                ConnectionCombo.SelectedItem = _original.ConnectionName;
            PassBox.PlaceholderText = "留空表示不修改原密码";
        }
        else
        {
            UserBox.Text = string.Empty;
            PassBox.Password = string.Empty;
        }
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
        catch (Exception ex)
        {
            Show("读取连接列表失败：" + ex.Message, InfoBarSeverity.Error);
        }
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => LoadConnections();

    private void OnConnectionSelected(object sender, SelectionChangedEventArgs e)
    {
        // 仅用于触发，取值在保存时进行
    }

    private void OnCreateConnection(object sender, RoutedEventArgs e)
    {
        string name = NewConnectionBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
        {
            Show("请输入新连接的名称。", InfoBarSeverity.Warning);
            return;
        }
        try
        {
            PhonebookManager.CreatePppoeEntry(name);
            Show($"已创建连接「{name}」。", InfoBarSeverity.Success);
            NewConnectionBox.Text = string.Empty;
            LoadConnections();
            ConnectionCombo.SelectedItem = name;
        }
        catch (Exception ex)
        {
            Show("创建连接失败：" + ex.Message, InfoBarSeverity.Error);
        }
    }

    private DialCredential? BuildCredential()
    {
        string? conn = ConnectionCombo.SelectedItem as string;
        if (string.IsNullOrEmpty(conn))
        {
            Show("请选择或创建一个拨号连接。", InfoBarSeverity.Warning);
            return null;
        }

        string user = UserBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(user))
        {
            Show("请输入校园网账号。", InfoBarSeverity.Warning);
            return null;
        }

        string pass = PassBox.Password ?? string.Empty;
        if (string.IsNullOrEmpty(pass))
        {
            if (_original != null && string.Equals(_original.ConnectionName, conn, StringComparison.OrdinalIgnoreCase))
                pass = _original.Password;
            else
            {
                Show("请输入密码。", InfoBarSeverity.Warning);
                return null;
            }
        }

        return new DialCredential
        {
            ConnectionName = conn,
            UserName = user,
            Password = pass
        };
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var cred = BuildCredential();
        if (cred == null) return;
        try
        {
            CredentialStore.Save(cred);
            Show("账号信息已保存（已加密）。", InfoBarSeverity.Success);
            _original = cred;
            PassBox.Password = string.Empty;
            PassBox.PlaceholderText = "留空表示不修改原密码";
        }
        catch (Exception ex)
        {
            Show("保存失败：" + ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void OnSaveAndTest(object sender, RoutedEventArgs e)
    {
        var cred = BuildCredential();
        if (cred == null) return;
        try
        {
            CredentialStore.Save(cred);
            _original = cred;
            Show("已保存，正在测试拨号…", InfoBarSeverity.Informational, false);
            var result = await Task.Run(async () => await _dialer.DialOnceAsync(cred));
            if (result.Success)
                Show($"测试成功，IP：{result.IpAddress}", InfoBarSeverity.Success);
            else
                Show(StatusPage.DescribeError(result.ErrorCode), InfoBarSeverity.Error);
            PassBox.Password = string.Empty;
        }
        catch (Exception ex)
        {
            Show("测试异常：" + ex.Message, InfoBarSeverity.Error);
        }
    }

    private void Show(string text, InfoBarSeverity severity, bool open = true)
    {
        MessageBar.Message = text;
        MessageBar.Severity = severity;
        MessageBar.IsOpen = open;
    }
}
