using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using KazakoraAgent.App.Services;
using KazakoraAgent.App.Theming;
using KazakoraAgent.App.ViewModels;
using KazakoraAgent.Core.Api;
using KazakoraAgent.Core.Printing;
using KazakoraAgent.Core.Queue;

namespace KazakoraAgent.App;

public partial class App : System.Windows.Application
{
    private DashboardPoller? _poller;
    private SqliteJobStore? _jobStore;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = AppSettings.Load();

        ThemeManager.SetTheme(settings.Theme == "Light" ? AppTheme.Light : AppTheme.Dark);

        var http = new HttpClient
        {
            BaseAddress = new Uri(settings.ApiBaseUrl),
        };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiToken);

        IKazakoraApiClient api = new KazakoraApiClient(http);

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KoraSync", "queue.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _jobStore = new SqliteJobStore($"Data Source={dbPath}");

        IPrinter printer = new WindowsPrinter();
        var retryPolicy = new RetryPolicy();

        var queueEngine = new QueueEngine(
            api,
            _jobStore,
            printer,
            retryPolicy,
            settings.AgentId,
            _ => settings.PrinterName);

        var mainViewModel = new MainViewModel(api);

        _poller = new DashboardPoller(
            api,
            _jobStore,
            queueEngine,
            mainViewModel,
            TimeSpan.FromSeconds(Math.Max(1, settings.QueuePollSeconds)),
            TimeSpan.FromSeconds(Math.Max(1, settings.DashboardPollSeconds)));

        var mainWindow = new MainWindow { DataContext = mainViewModel };
        MainWindow = mainWindow;
        mainWindow.Show();

        _poller.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _poller?.Dispose();
        _jobStore?.Dispose();

        base.OnExit(e);
    }
}
