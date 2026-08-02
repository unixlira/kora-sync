using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Threading;
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
    private TrayIconService? _tray;
    private AppSettings? _settings;
    private MainWindow? _mainWindow;
    private DispatcherTimer? _cleanupTimer;
    private bool _isExiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Fechar a última janela (dashboard escondido na bandeja) não deve
        // encerrar o processo — só "Sair" no menu da bandeja encerra de verdade.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settings = AppSettings.Load();
        _settings = settings;

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

        var poller = new DashboardPoller(
            api,
            _jobStore,
            queueEngine,
            mainViewModel,
            TimeSpan.FromSeconds(Math.Max(1, settings.QueuePollSeconds)),
            TimeSpan.FromSeconds(Math.Max(1, settings.DashboardPollSeconds)));
        _poller = poller;

        var tray = new TrayIconService();
        _tray = tray;
        tray.OpenDashboardRequested += ShowMainWindow;
        tray.OpenSettingsRequested += OpenSettings;
        tray.OpenAboutRequested += OpenAbout;
        tray.QueuePauseToggled += paused => poller.IsPaused = paused;
        tray.ExitRequested += () =>
        {
            _isExiting = true;
            Shutdown();
        };

        if (settings.NotificationsEnabled)
        {
            queueEngine.JobFailedPermanently += job =>
                Dispatcher.Invoke(() => tray.ShowError(
                    "Falha permanente na impressão",
                    $"Pedido #{job.OrderId}: {job.LastError ?? "esgotou as tentativas"}"));

            poller.ConnectionLost += () =>
                Dispatcher.Invoke(() => tray.ShowError("Conexão perdida", "Não foi possível falar com o servidor da Kazakora."));

            poller.ConnectionRestored += () =>
                Dispatcher.Invoke(() => tray.ShowInfo("Conexão restaurada", "A comunicação com o servidor voltou ao normal."));
        }

        var mainWindow = new MainWindow { DataContext = mainViewModel };
        _mainWindow = mainWindow;
        mainWindow.Closing += (_, args) =>
        {
            if (_isExiting)
            {
                return;
            }

            // "Fechar" minimiza pra bandeja em vez de encerrar — o app
            // precisa continuar rodando em segundo plano processando a fila.
            args.Cancel = true;
            mainWindow.Hide();
        };

        MainWindow = mainWindow;
        mainWindow.Show();

        poller.Start();

        // Roda uma vez já na abertura (cobre quem reinicia o app com
        // frequência) e depois 1x/dia (cobre quem deixa rodando 24/7 sem
        // nunca reiniciar) — sem isso o banco local cresceria pra sempre.
        var retention = TimeSpan.FromDays(Math.Max(1, settings.QueueRetentionDays));
        _ = CleanupOldJobsAsync(_jobStore, retention);

        _cleanupTimer = new DispatcherTimer { Interval = TimeSpan.FromDays(1) };
        _cleanupTimer.Tick += async (_, _) => await CleanupOldJobsAsync(_jobStore, retention);
        _cleanupTimer.Start();
    }

    private static async Task CleanupOldJobsAsync(SqliteJobStore jobStore, TimeSpan retention)
    {
        try
        {
            await jobStore.DeleteOldTerminalJobsAsync(DateTimeOffset.UtcNow - retention);
        }
        catch
        {
            // Limpeza é manutenção, não caminho crítico — falhar aqui não
            // pode derrubar o app nem interromper a fila.
        }
    }

    private void ShowMainWindow()
    {
        _mainWindow?.Show();
        _mainWindow?.Activate();

        if (_mainWindow?.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }
    }

    private void OpenSettings()
    {
        if (_settings is null)
        {
            return;
        }

        var window = new SettingsWindow(_settings) { Owner = _mainWindow };
        window.ShowDialog();
    }

    private void OpenAbout()
    {
        var window = new AboutWindow { Owner = _mainWindow };
        window.ShowDialog();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _cleanupTimer?.Stop();
        _poller?.Dispose();
        _jobStore?.Dispose();
        _tray?.Dispose();

        base.OnExit(e);
    }
}
