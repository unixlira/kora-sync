using System.Windows.Threading;
using KazakoraAgent.App.ViewModels;
using KazakoraAgent.Core.Api;
using KazakoraAgent.Core.Queue;

namespace KazakoraAgent.App.Services;

/// <summary>
/// Três timers de cadência diferente, de propósito (ver conversa sobre
/// tempo real vs polling): a fila de impressão usa um intervalo curto
/// (padrão 1s — velocidade importa, é dinheiro/pedido esperando), o
/// dashboard (métricas/cards do topo) usa um intervalo mais espaçado
/// (padrão 2s — é só visual), e o texto diário usa um intervalo bem mais
/// longo (padrão 30min — o texto só muda 1x por dia no servidor, não faz
/// sentido nem consultar isso na mesma cadência do resto). Todos rodam na
/// thread de UI (DispatcherTimer), então atualizar as ObservableCollections
/// aqui é seguro sem Dispatcher.Invoke manual.
/// </summary>
public sealed class DashboardPoller : IDisposable
{
    private readonly IKazakoraApiClient _api;
    private readonly QueueEngine _queueEngine;
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _queueTimer;
    private readonly DispatcherTimer _dashboardTimer;
    private readonly DispatcherTimer _dailyTextTimer;

    private bool _queueTickRunning;
    private bool _dashboardTickRunning;
    private bool _dailyTextTickRunning;
    private bool? _wasReachable;

    /// Pausada via bandeja ("Pausar Fila") — o timer continua rodando (o
    /// processamento em si é quem para; não há mais lista visual pra
    /// atualizar aqui desde que a fila/etiquetas saíram do layout,
    /// 2026-08-06), só não sincroniza nem processa novos jobs enquanto
    /// pausado.
    public bool IsPaused { get; set; }

    /// Perda de conectividade com a API detectada (dispara notificação do Windows).
    public event Action? ConnectionLost;

    public event Action? ConnectionRestored;

    public DashboardPoller(
        IKazakoraApiClient api,
        QueueEngine queueEngine,
        MainViewModel viewModel,
        TimeSpan? queueInterval = null,
        TimeSpan? dashboardInterval = null,
        TimeSpan? dailyTextInterval = null)
    {
        _api = api;
        _queueEngine = queueEngine;
        _viewModel = viewModel;

        _queueTimer = new DispatcherTimer { Interval = queueInterval ?? TimeSpan.FromSeconds(1) };
        _queueTimer.Tick += async (_, _) => await QueueTickAsync();

        _dashboardTimer = new DispatcherTimer { Interval = dashboardInterval ?? TimeSpan.FromSeconds(5) };
        _dashboardTimer.Tick += async (_, _) => await DashboardTickAsync();

        _dailyTextTimer = new DispatcherTimer { Interval = dailyTextInterval ?? TimeSpan.FromMinutes(30) };
        _dailyTextTimer.Tick += async (_, _) => await DailyTextTickAsync();
    }

    public void Start()
    {
        _queueTimer.Start();
        _dashboardTimer.Start();
        _dailyTextTimer.Start();
        // Busca já na inicialização, sem esperar o primeiro intervalo de
        // 30min — senão o texto ficaria em branco por meia hora toda vez
        // que o app abre.
        _ = DailyTextTickAsync();
    }

    public void Stop()
    {
        _queueTimer.Stop();
        _dashboardTimer.Stop();
        _dailyTextTimer.Stop();
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
            if (!IsPaused)
            {
                await _queueEngine.SyncFromServerAsync();

                // Drena tudo que já está pronto agora, não só um job por tick —
                // com vários pedidos chegando juntos (Shopee+ML+TikTok), não faz
                // sentido esperar mais 1s pra cada um.
                while (await _queueEngine.ProcessNextDueJobAsync())
                {
                }
            }
        }
        catch (Exception ex)
        {
            // Falha de rede/API num ciclo de fila não deve derrubar o app —
            // o próximo tick tenta de novo. O card de canal já sinaliza
            // "não alcançável" pelo lado do dashboard timer. Logada agora
            // (2026-08-07) — antes sumia sem deixar rastro nenhum.
            KazakoraAgent.Core.AppLog.Error($"QueueTick falhou: {ex.Message}");
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
            // Uma chamada por bloco try/catch, de propósito — antes uma
            // única exceção em qualquer uma delas derrubava o tick inteiro
            // sem sinalizar nada além da bolinha de status ficar amarela.
            // Cada falha agora fica registrada em LastDashboardError
            // (exibido na tela, ver MainWindow.xaml) em vez de sumir sem
            // explicação. (A busca de etiquetas que existia aqui saiu
            // junto com a lista "ETIQUETAS" do layout, 2026-08-06 — não
            // faz mais sentido consultar esse endpoint a cada tick sem
            // nada na tela pra mostrar o resultado.)
            var anyFailure = false;

            try
            {
                var channels = await _api.GetChannelsAsync();
                _viewModel.UpdateChannels(channels);
            }
            catch (Exception ex)
            {
                anyFailure = true;
                _viewModel.LastDashboardError = $"Canais: {ex.Message}";
            }

            try
            {
                var metrics = await _api.GetMetricsAsync();
                _viewModel.UpdateMetrics(metrics);
            }
            catch (Exception ex)
            {
                anyFailure = true;
                _viewModel.LastDashboardError = $"Métricas: {ex.Message}";
            }

            try
            {
                // Fila de expedição de hoje (pedido explícito 2026-08-06) —
                // mesma cadência de 2s do resto do dashboard, não a de 1s
                // do QueueTickAsync (que é sobre PROCESSAR/imprimir, não
                // sobre o que aparece na tela).
                var queue = await _api.GetOrderQueueAsync();
                _viewModel.UpdateOrderQueue(queue);
            }
            catch (Exception ex)
            {
                anyFailure = true;
                _viewModel.LastDashboardError = $"Fila do dia: {ex.Message}";
            }

            try
            {
                // Vendas agendadas pelo canal (pedido explícito 2026-08-14,
                // achado no pedido #278) — muda raramente (só quando um
                // pedido novo agendado chega ou um agendado sai da lista),
                // mas manter na mesma cadência de 2s do resto é mais simples
                // que inventar um timer à parte só pra isso.
                var scheduled = await _api.GetScheduledShipmentsAsync();
                _viewModel.UpdateScheduledShipments(scheduled);
            }
            catch (Exception ex)
            {
                anyFailure = true;
                _viewModel.LastDashboardError = $"Envios agendados: {ex.Message}";
            }

            if (!anyFailure)
            {
                if (_wasReachable == false)
                {
                    ConnectionRestored?.Invoke();
                }

                _wasReachable = true;
                _viewModel.LastDashboardError = null;
            }
            else
            {
                if (_wasReachable != false)
                {
                    ConnectionLost?.Invoke();
                }

                _wasReachable = false;
            }
        }
        finally
        {
            _dashboardTickRunning = false;
        }
    }

    private async Task DailyTextTickAsync()
    {
        if (_dailyTextTickRunning)
        {
            return;
        }

        _dailyTextTickRunning = true;

        try
        {
            var dailyText = await _api.GetDailyTextAsync();
            _viewModel.UpdateDailyText(dailyText);
        }
        catch
        {
            // Falha aqui não é crítica (não é dado operacional) — mantém o
            // último texto já mostrado em vez de sumir/mostrar erro; o
            // próximo tick de 30min tenta de novo.
        }
        finally
        {
            _dailyTextTickRunning = false;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
