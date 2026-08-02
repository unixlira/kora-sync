using System.Windows;
using KazakoraAgent.App.Services;

namespace KazakoraAgent.App;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        _settings = settings;

        ApiBaseUrlBox.Text = settings.ApiBaseUrl;
        ApiTokenBox.Text = settings.ApiToken;
        PrinterNameBox.Text = settings.PrinterName;
        QueuePollSecondsBox.Text = settings.QueuePollSeconds.ToString();
        DashboardPollSecondsBox.Text = settings.DashboardPollSeconds.ToString();
        NotificationsEnabledBox.IsChecked = settings.NotificationsEnabled;
        StartWithWindowsBox.IsChecked = settings.StartWithWindows;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.ApiBaseUrl = ApiBaseUrlBox.Text.Trim();
        _settings.ApiToken = ApiTokenBox.Text.Trim();
        _settings.PrinterName = PrinterNameBox.Text.Trim();
        _settings.NotificationsEnabled = NotificationsEnabledBox.IsChecked ?? true;
        _settings.StartWithWindows = StartWithWindowsBox.IsChecked ?? false;

        if (int.TryParse(QueuePollSecondsBox.Text, out var queueSeconds) && queueSeconds > 0)
        {
            _settings.QueuePollSeconds = queueSeconds;
        }

        if (int.TryParse(DashboardPollSecondsBox.Text, out var dashboardSeconds) && dashboardSeconds > 0)
        {
            _settings.DashboardPollSeconds = dashboardSeconds;
        }

        _settings.Save();
        StartupRegistration.Apply(_settings.StartWithWindows);

        MessageBox.Show("Configurações salvas. Reinicie o app pra aplicar.", "KoraSync",
            MessageBoxButton.OK, MessageBoxImage.Information);

        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
