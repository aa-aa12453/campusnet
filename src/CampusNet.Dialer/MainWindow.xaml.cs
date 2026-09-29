using System.Windows;
using CampusNet.Core.Credentials;
using CampusNet.Dialer.Pages;

namespace CampusNet.Dialer;

public partial class MainWindow
{
    private bool _firstRunHandled;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RootNavigation.Navigate(typeof(StatusPage));

        if (!_firstRunHandled && !CredentialStore.Exists())
        {
            _firstRunHandled = true;
            var wizard = new FirstRunWizard { Owner = this };
            wizard.ShowDialog();
        }
    }
}
