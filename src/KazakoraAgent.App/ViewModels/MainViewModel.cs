using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using KazakoraAgent.Core;
using KazakoraAgent.Core.Api;
using KazakoraAgent.Core.Models;
using MahApps.Metro.IconPacks;

namespace KazakoraAgent.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// Null só em cenário de design-time/teste sem API de verdade (ver
    /// construtor sem argumento) — o botão "Em preparação" de cada card
    /// (OrderQueueCardViewModel.PackRequested) simplesmente não faz nada
    /// nesse caso, em vez de estourar NullReferenceException.
    private readonly IKazakoraApiClient? _api;

    /// Um card por métrica do topo — mantido em sincronia com Metrics
    /// sempre que UpdateMetrics roda (ver DashboardTickAsync no poller).
    public ObservableCollection<MetricCardViewModel> MetricCards { get; }

    /// 5º card do topo — "Pedidos por canal", atualizado junto com
    /// UpdateChannels (mesmo tick, mesma fonte de dados).
    public ChannelOrdersCardViewModel ChannelOrdersCard { get; } = new();

    /// União ordenada dos 4 MetricCardViewModel + o ChannelOrdersCard, na
    /// ordem exata em que devem aparecer na UniformGrid do topo — a XAML
    /// usa DataTemplate implícito por tipo (sem ItemTemplate fixo) pra
    /// renderizar os dois tipos misturados na mesma linha.
    public ObservableCollection<object> TopCards { get; }

    /// Os 3 cards em destaque da fila de expedição de hoje — pedido mais
    /// recente (QueueCard1), segundo (QueueCard2) e terceiro (QueueCard3);
    /// eram só 2 até 2026-08-06, ampliado pra 3 em pedido explícito
    /// 2026-08-15 junto com a foto do produto. Instâncias fixas (não uma
    /// ObservableCollection) porque a XAML liga cada uma direto num Border
    /// próprio, não via ItemsControl — mais simples de posicionar 3
    /// "quadrados" fixos empilhados. Ver UpdateOrderQueueAsync.
    public OrderQueueCardViewModel QueueCard1 { get; } = new();

    public OrderQueueCardViewModel QueueCard2 { get; } = new();

    public OrderQueueCardViewModel QueueCard3 { get; } = new();

    /// 4º pedido do dia em diante, mesma ordem decrescente — lista com
    /// scroll próprio (coluna da direita, ver MainWindow.xaml). Sem foto de
    /// produto de propósito (pedido explícito 2026-08-15): só os 3 cards em
    /// destaque carregam imagem, esta lista compacta continua só texto —
    /// baixar imagem pra cada item de uma lista que pode ter dezenas de
    /// pedidos custaria banda/tempo sem ganho real (já não cabe grande o
    /// suficiente pra ajudar na conferência visual mesmo).
    public ObservableCollection<OrderQueueCardViewModel> QueueRest { get; } = [];

    /// Vendas AGENDADAS pelo canal (pedido explícito 2026-08-14, achado no
    /// pedido #278) — banner separado da fila normal, só aparece quando
    /// tem algo (ver HasScheduledShipments/MainWindow.xaml). Já vem do
    /// servidor ordenado pela data mais próxima primeiro.
    public ObservableCollection<ScheduledShipmentCardViewModel> ScheduledShipments { get; } = [];

    public bool HasScheduledShipments => ScheduledShipments.Count > 0;

    public bool HasOverdueScheduledShipments => ScheduledShipments.Any(s => s.IsOverdue);

    public Visibility ScheduledShipmentsVisibility => HasScheduledShipments ? Visibility.Visible : Visibility.Collapsed;

    /// Mensagem da última falha ao buscar canais/métricas — null quando o
    /// último tick foi bem-sucedido. Ver DashboardPoller.DashboardTickAsync.
    [ObservableProperty]
    private string? _lastDashboardError;

    public Visibility HasDashboardErrorVisibility => LastDashboardError is null ? Visibility.Collapsed : Visibility.Visible;

    partial void OnLastDashboardErrorChanged(string? value) => OnPropertyChanged(nameof(HasDashboardErrorVisibility));

    /// Texto diário das Testemunhas de Jeová — null até o primeiro fetch
    /// (DashboardPoller busca já na inicialização, ver Start()) ou se o
    /// servidor ainda não tem nenhuma linha salva. Mostra o versículo em
    /// si (DailyTextQuote) com a referência bíblica sempre na linha de
    /// baixo (DailyTextReference, pedido explícito 2026-08-04 — os dois
    /// já vêm separados do servidor, ver DailyTextFetcherService no
    /// Laravel) — não a data (isso já aparece no relógio à direita) nem o
    /// comentário completo (fonte pequena no meio do cabeçalho, não cabe).
    [ObservableProperty]
    private string? _dailyTextQuote;

    [ObservableProperty]
    private string? _dailyTextReference;

    public Visibility HasDailyTextVisibility => DailyTextQuote is null ? Visibility.Collapsed : Visibility.Visible;

    partial void OnDailyTextQuoteChanged(string? value) => OnPropertyChanged(nameof(HasDailyTextVisibility));

    /// Relógio em tempo real do cabeçalho — atualizado a cada segundo por
    /// um DispatcherTimer dedicado em MainWindow.xaml.cs (puramente local,
    /// sem chamada de rede, por isso não faz parte do DashboardPoller).
    [ObservableProperty]
    private string _currentDateTimeText = string.Empty;

    public MainViewModel(IKazakoraApiClient? api = null)
    {
        _api = api;

        var resources = Application.Current.Resources;

        var successBrush = (Brush) resources["StatusSuccessBrush"];
        var processingBrush = (Brush) resources["StatusProcessingBrush"];
        var purpleBrush = new SolidColorBrush((Color) ColorConverter.ConvertFromString("#7B61FF")!);
        var errorBrush = (Brush) resources["StatusErrorBrush"];
        var neutralBrush = (Brush) resources["TextPrimaryBrush"];

        // 4 cards fixos (visão geral, todas as plataformas somadas) — ver
        // UpdateMetrics pro preenchimento. Índices usados lá são
        // posicionais de propósito (mesmo padrão já existente antes desta
        // mudança), então a ordem aqui importa.
        MetricCards =
        [
            new MetricCardViewModel { Label = "Faturamento do mês", NumberBrush = neutralBrush, AccentBrush = successBrush, IconKind = PackIconMaterialKind.TrendingUp, VariationSuffix = "vs mês anterior" },
            new MetricCardViewModel { Label = "Faturamento de hoje", NumberBrush = neutralBrush, AccentBrush = processingBrush, IconKind = PackIconMaterialKind.CalendarClock, VariationSuffix = "vs ontem" },
            new MetricCardViewModel { Label = "Pedidos hoje", NumberBrush = neutralBrush, AccentBrush = purpleBrush, IconKind = PackIconMaterialKind.PackageVariantClosed, VariationSuffix = "vs ontem" },
            new MetricCardViewModel { Label = "Devoluções do mês", NumberBrush = neutralBrush, AccentBrush = errorBrush, IconKind = PackIconMaterialKind.KeyboardReturn },
        ];

        TopCards = [.. MetricCards, ChannelOrdersCard];

        // Os 3 cards fixos precisam do callback já na construção — os da
        // QueueRest recebem o mesmo callback quando são criados, ver
        // UpdateOrderQueueAsync.
        QueueCard1.PackRequested = PackOrderAsync;
        QueueCard2.PackRequested = PackOrderAsync;
        QueueCard3.PackRequested = PackOrderAsync;
    }

    public void UpdateMetrics(DashboardMetricsDto dto)
    {
        MetricCards[0].Value = dto.RevenueMonth.ToString("C2", PtBr);
        MetricCards[0].PeriodLabel = dto.MonthLabel;
        MetricCards[0].VariationPct = dto.RevenueMonthVariationPct;

        MetricCards[1].Value = dto.RevenueToday.ToString("C2", PtBr);
        MetricCards[1].PeriodLabel = dto.TodayLabel;
        MetricCards[1].VariationPct = dto.RevenueTodayVariationPct;

        MetricCards[2].Value = dto.SalesToday.ToString(PtBr);
        MetricCards[2].PeriodLabel = dto.TodayLabel;
        MetricCards[2].VariationPct = dto.SalesTodayVariationPct;

        MetricCards[3].Value = dto.ReturnsMonth.ToString(PtBr);
        MetricCards[3].PeriodLabel = dto.MonthLabel;
    }

    public void UpdateDailyText(DailyTextDto? dto)
    {
        DailyTextQuote = dto?.ScriptureQuote;
        DailyTextReference = dto?.ScriptureReference;
    }

    public void UpdateChannels(IReadOnlyList<ChannelStatusDto> statuses)
    {
        ChannelOrdersCard.UpdateFrom(statuses);
    }

    /// <summary>
    /// items já vem do servidor em ordem decrescente (ver
    /// DashboardAgentController::queue, orderByDesc('id')). QueueCard1/2/3
    /// são reaproveitados em vez de recriados a cada tick — só os campos
    /// mudam — pra não perder o estado de scroll/seleção de nada que
    /// dependa de identidade de objeto no futuro; QueueRest é reconstruída
    /// (mais simples, e a lista muda de tamanho a cada pedido novo/enviado
    /// de qualquer forma).
    ///
    /// Async desde 2026-08-15 (pedido explícito, foto do produto): cada
    /// card em destaque baixa a imagem só quando o PEDIDO daquele slot
    /// muda (comparação feita ANTES de UpdateFrom sobrescrever OrderId) —
    /// sem isso, o app rebaixaria a mesma imagem a cada tick de 2s pra
    /// sempre, puro desperdício de banda/tempo pro mesmo pedido parado no
    /// mesmo card. Já reusado (tick anterior) continua com a imagem que já
    /// tinha, sem re-download nem piscar.
    /// </summary>
    public async Task UpdateOrderQueueAsync(IReadOnlyList<OrderQueueItemDto> items)
    {
        await UpdateFeaturedCardAsync(QueueCard1, items.Count > 0 ? items[0] : null);
        await UpdateFeaturedCardAsync(QueueCard2, items.Count > 1 ? items[1] : null);
        await UpdateFeaturedCardAsync(QueueCard3, items.Count > 2 ? items[2] : null);

        QueueRest.Clear();
        foreach (var dto in items.Skip(3))
        {
            var item = new OrderQueueCardViewModel { PackRequested = PackOrderAsync };
            item.UpdateFrom(dto);
            QueueRest.Add(item);
        }
    }

    private async Task UpdateFeaturedCardAsync(OrderQueueCardViewModel card, OrderQueueItemDto? dto)
    {
        if (dto is null)
        {
            card.Clear();

            return;
        }

        var orderChanged = card.OrderId != dto.Id;

        card.UpdateFrom(dto);

        if (!orderChanged)
        {
            return;
        }

        // Limpa a foto antiga NA HORA (não espera o download terminar) —
        // sem isso, o card mostraria a foto do pedido anterior colada no
        // pedido novo por alguns instantes, exatamente o tipo de confusão
        // visual que esta feature existe pra evitar.
        card.SetProductImage(null);

        if (_api is null)
        {
            return;
        }

        try
        {
            var bytes = await _api.DownloadOrderImageAsync(dto.Id);
            card.SetProductImage(bytes);
        }
        catch (Exception ex)
        {
            // Imagem é só apoio visual — falha de rede aqui não pode virar
            // LastDashboardError nem derrubar o resto do tick (ver
            // DashboardPoller.DashboardTickAsync, que já trata separado
            // canais/métricas/fila/agendados); só fica sem foto.
            AppLog.Error($"Falha ao baixar a imagem do pedido #{dto.Id}: {ex.Message}");
        }
    }

    /// Callback por trás do botão "Em preparação" de todo card da fila
    /// (pedido explícito 2026-08-13) — chamado por
    /// OrderQueueCardViewModel.PackCommand. Só confirma no servidor; o
    /// próprio card vira "Embalado" sozinho assim que este await termina
    /// sem erro (ver OrderQueueCardViewModel.PackAsync) — diferente da
    /// primeira versão deste botão, o pedido NÃO sai da fila, então não há
    /// motivo pra buscar a lista inteira de novo aqui.
    private Task PackOrderAsync(long orderId) => _api?.PackOrderAsync(orderId) ?? Task.CompletedTask;

    /// Banner "Envios agendados" (pedido explícito 2026-08-14) — reconstruída
    /// a cada tick, mesmo padrão do QueueRest (lista muda de tamanho a
    /// qualquer momento, mais simples que reconciliar item por item).
    public void UpdateScheduledShipments(IReadOnlyList<ScheduledShipmentDto> items)
    {
        ScheduledShipments.Clear();

        foreach (var dto in items)
        {
            ScheduledShipments.Add(ScheduledShipmentCardViewModel.FromDto(dto));
        }

        OnPropertyChanged(nameof(HasScheduledShipments));
        OnPropertyChanged(nameof(HasOverdueScheduledShipments));
        OnPropertyChanged(nameof(ScheduledShipmentsVisibility));
    }
}
