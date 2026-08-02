using System.Windows.Threading;
using KazakoraAgent.App.ViewModels;
using KazakoraAgent.Core.Api;
using KazakoraAgent.Core.Queue;

namespace KazakoraAgent.App.Services;

/// <summary>
/// Dois timers de cadência diferente, de propósito (ver conversa sobre
/// tempo real vs polling): a fila de impressão usa um intervalo curto
/// (padrão 1s — velocidade importa, é dinheiro/pedido esperando), o
/// dashboard (métricas/cards) usa um intervalo mais espaçado (padrão 5s —
/// é só visual, bater 1x/s nisso o dia todo é carga desnecessária no banco
/// numa hospedagem compartilhada). Ambos rodam na thread de UI
/// (DispatcherTimer), então atualizar as ObservableCollections aqui é
/// seguro sem Dispatcher.Invoke manual.
/// </summary>
public sealed class DashboardPoller : IDisposable
{
    private readonly IKazakoraApiClient _api;
    private readonly IJobStore _jobStore;
    private readonly QueueEngine _queueEngine;
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _queueTimer;
    private readonly DispatcherTimer _dashboardTimer;

    private bool _queueTickRunning;
    private bool _dashboardTickRunning;

    public DashboardPoller(
        IKazakoraApiClient api,
        IJobStore jobStore,
        QueueEngine queueEngine,
        MainViewModel viewModel,
        TimeSpan? queueInterval = null,
        TimeSpan? dashboardInterval = null)
    {
        _api = api;
        _jobStore = jobStore;
        _queueEngine = queueEngine;
        _viewModel = viewModel;

        _queueTimer = new DispatcherTimer { Interval = queueInterval ?? TimeSpan.FromSeconds(1) };
        _queueTimer.Tick += async (_, _) => await QueueTickAsync();

        _dashboardTimer = new DispatcherTimer { Interval = dashboardInterval ?? TimeSpan.FromSeconds(5) };
        _dashboardTimer.Tick += async (_, _) => await DashboardTickAsync();
    }

    public void Start()
    {
        _queueTimer.Start();
        _dashboardTimer.Start();
    }

    public void Stop()
    {
        _queueTimer.Stop();
        _dashboardTimer.Stop();
    }

    private async Task QueueTickAsync()
    {
        // Evita sobreposição se um ciclo demorar mais que o intervalo —
        // o próximo tick só entra depois que o anterior terminou.
        if (_queueTickRunning)
        {
            return;
        }

        _queueTickRunning = true;

        try
        {
            await _queueEngine.SyncFromServerAsync();

            // Drena tudo que já está pronto agora, não só um job por tick —
            // com vários pedidos chegando juntos (Shopee+ML+TikTok), não faz
            // sentido esperar mais 1s pra cada um.
            while (await _queueEngine.ProcessNextDueJobAsync())
            {
            }

            var jobs = await _jobStore.GetAllAsync();
            _viewModel.ReplaceQueueItems(jobs.Select(QueueItemViewModel.FromDomain));
        }
        catch
        {
            // Falha de rede/API num ciclo de fila não deve derrubar o app —
            // o próximo tick tenta de novo. O card de canal já sinaliza
            // "não alcançável" pelo lado do dashboard timer.
        }
        finally
        {
            _queueTickRunning = false;
        }
    }

    private async Task DashboardTickAsync()
    {
        if (_dashboardTickRunning)
        {
            return;
        }

        _dashboardTickRunning = true;

        try
        {
            var channels = await _api.GetChannelsAsync();
            _viewModel.UpdateChannels(channels);

            var metrics = await _api.GetMetricsAsync();
            _viewModel.UpdateMetrics(metrics);
        }
        catch
        {
            _viewModel.MarkChannelsUnreachable();
        }
        finally
        {
            _dashboardTickRunning = false;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
