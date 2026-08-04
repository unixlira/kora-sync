using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KazakoraAgent.App.Theming;
using KazakoraAgent.Core.Api;
using KazakoraAgent.Core.Models;
using KazakoraAgent.Core.Queue;
using MahApps.Metro.IconPacks;

namespace KazakoraAgent.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IKazakoraApiClient _api;
    private readonly QueueEngine _queueEngine;

    public MetricsViewModel Metrics { get; } = new();

    public ObservableCollection<ChannelCardViewModel> Channels { get; }

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

    /// Lista de etiquetas (produto/SKU/pedido), mais recentes no topo —
    /// populada por ReplaceLabels a cada tick do dashboard (ver
    /// DashboardPoller). Uma linha por produto, não por PrintJob.
    public ObservableCollection<LabelItemViewModel> Labels { get; } = [];

    public ObservableCollection<QueueItemViewModel> QueueWaiting { get; } = [];

    public ObservableCollection<QueueItemViewModel> QueueProcessing { get; } = [];

    public ObservableCollection<QueueItemViewModel> QueueCompletedToday { get; } = [];

    public ObservableCollection<QueueItemViewModel> QueueFailedOrRetrying { get; } = [];

    /// Um card por status da fila (mesmo padrão visual do painel de
    /// Impressões do Casa Cora) — Count é sincronizado com as 4 coleções
    /// acima toda vez que ReplaceQueueItems roda.
    public ObservableCollection<QueueStatusCardViewModel> QueueStatusCards { get; }

    /// Mensagem da última falha ao buscar canais/métricas — null quando o
    /// último tick foi bem-sucedido. Ver DashboardPoller.DashboardTickAsync.
    [ObservableProperty]
    private string? _lastDashboardError;

    public Visibility HasDashboardErrorVisibility => LastDashboardError is null ? Visibility.Collapsed : Visibility.Visible;

    partial void OnLastDashboardErrorChanged(string? value) => OnPropertyChanged(nameof(HasDashboardErrorVisibility));

    /// Mesma ideia, mas só pra falha específica em buscar a lista de
    /// etiquetas — separado de LastDashboardError porque canais/métricas
    /// podem estar OK enquanto só a lista falha (ou vice-versa).
    [ObservableProperty]
    private string? _lastLabelsError;

    public Visibility HasLabelsErrorVisibility => LastLabelsError is null ? Visibility.Collapsed : Visibility.Visible;

    partial void OnLastLabelsErrorChanged(string? value) => OnPropertyChanged(nameof(HasLabelsErrorVisibility));

    [ObservableProperty]
    private bool _isShowingDetail;

    [ObservableProperty]
    private ChannelDetailViewModel? _selectedDetail;

    public IRelayCommand CloseDetailCommand { get; }

    /// Aponta pra uma das 4 coleções acima (mesma instância, não uma cópia)
    /// — assim a listagem de detalhe continua ao vivo enquanto está aberta.
    [ObservableProperty]
    private ObservableCollection<QueueItemViewModel>? _selectedQueueItems;

    [ObservableProperty]
    private string? _queueDetailTitle;

    [ObservableProperty]
    private bool _isShowingQueueDetail;

    public IRelayCommand CloseQueueDetailCommand { get; }

    public MainViewModel(IKazakoraApiClient api, QueueEngine queueEngine)
    {
        _api = api;
        _queueEngine = queueEngine;

        Channels = new ObservableCollection<ChannelCardViewModel>(
            MarketplaceChannel.All.Select(channel => new ChannelCardViewModel(channel, OpenChannelDetail)));

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

        QueueStatusCards =
        [
            new QueueStatusCardViewModel
            {
                Label = "Na fila", Description = "Aguardando o KoraSync processar.",
                IconKind = PackIconMaterialKind.TrayFull, AccentBrush = QueueStatusColors.Warning.Accent,
                CardBackgroundBrush = QueueStatusColors.Warning.SoftBackground,
                CardBorderBrush = QueueStatusColors.Warning.SoftBorder,
                OpenCommand = new RelayCommand(() => OpenQueueDetail(QueueWaiting, "Na fila")),
            },
            new QueueStatusCardViewModel
            {
                Label = "Imprimindo", Description = "Sendo processado agora mesmo.",
                IconKind = PackIconMaterialKind.Printer, AccentBrush = QueueStatusColors.Processing.Accent,
                CardBackgroundBrush = QueueStatusColors.Processing.SoftBackground,
                CardBorderBrush = QueueStatusColors.Processing.SoftBorder,
                OpenCommand = new RelayCommand(() => OpenQueueDetail(QueueProcessing, "Imprimindo")),
            },
            new QueueStatusCardViewModel
            {
                Label = "Concluídas hoje", Description = "Etiquetas impressas com sucesso.",
                IconKind = PackIconMaterialKind.CheckCircleOutline, AccentBrush = QueueStatusColors.Success.Accent,
                CardBackgroundBrush = QueueStatusColors.Success.SoftBackground,
                CardBorderBrush = QueueStatusColors.Success.SoftBorder,
                OpenCommand = new RelayCommand(() => OpenQueueDetail(QueueCompletedToday, "Concluídas hoje")),
            },
            new QueueStatusCardViewModel
            {
                Label = "Falharam", Description = "Erro ao processar ou aguardando nova tentativa.",
                IconKind = PackIconMaterialKind.AlertCircleOutline, AccentBrush = QueueStatusColors.Error.Accent,
                CardBackgroundBrush = QueueStatusColors.Error.SoftBackground,
                CardBorderBrush = QueueStatusColors.Error.SoftBorder,
                OpenCommand = new RelayCommand(() => OpenQueueDetail(QueueFailedOrRetrying, "Falharam")),
            },
        ];

        CloseDetailCommand = new RelayCommand(() =>
        {
            IsShowingDetail = false;
            SelectedDetail = null;
        });

        CloseQueueDetailCommand = new RelayCommand(() =>
        {
            IsShowingQueueDetail = false;
            SelectedQueueItems = null;
        });
    }

    public void UpdateMetrics(DashboardMetricsDto dto)
    {
        Metrics.UpdateFrom(dto);

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

    /// Controle de empacotamento: PrintJob.Id de cada etiqueta já conferida
    /// pelo funcionário (botão X) — só existe em memória (some ao reabrir o
    /// KoraSync), nunca é enviado pro servidor. O registro real continua
    /// intacto no banco do Kazakora, isso só filtra o que aparece na tela.
    private readonly HashSet<long> _dismissedLabelJobIds = [];

    /// <summary>Mais recentes no topo — LabelDto já vem ordenado assim do servidor (latest('id')).</summary>
    public void ReplaceLabels(IEnumerable<LabelDto> labels)
    {
        Labels.Clear();

        foreach (var dto in labels)
        {
            if (_dismissedLabelJobIds.Contains(dto.Id))
            {
                continue;
            }

            foreach (var item in LabelItemViewModel.FromDto(dto, RequestPrintAsync, DismissLabel))
            {
                Labels.Add(item);
            }
        }
    }

    /// Chamado pelo botão X de cada etiqueta — some da tela imediatamente
    /// (sem esperar o próximo tick de 2s) e fica marcado pra não voltar a
    /// aparecer nos próximos ciclos, mesmo que o servidor continue
    /// devolvendo o mesmo PrintJob (que nunca é apagado por isso).
    private void DismissLabel(long jobId)
    {
        _dismissedLabelJobIds.Add(jobId);

        var toRemove = Labels.Where(l => l.JobId == jobId).ToList();

        foreach (var item in toRemove)
        {
            Labels.Remove(item);
        }
    }

    private Task<bool> RequestPrintAsync(long jobId) => _queueEngine.RequestImmediateRetryAsync(jobId);

    public void UpdateChannels(IReadOnlyList<ChannelStatusDto> statuses)
    {
        var byChannel = statuses.ToDictionary(s => s.Channel);

        ChannelOrdersCard.UpdateFrom(statuses);

        foreach (var card in Channels)
        {
            if (byChannel.TryGetValue(card.Channel, out var dto))
            {
                card.UpdateFrom(dto);
            }
            else
            {
                card.MarkUnreachable();
            }
        }
    }

    public void MarkChannelsUnreachable()
    {
        foreach (var card in Channels)
        {
            card.MarkUnreachable();
        }
    }

    public void ReplaceQueueItems(IEnumerable<QueueItemViewModel> items)
    {
        var today = DateTimeOffset.Now.Date;
        var materialized = items as IReadOnlyCollection<QueueItemViewModel> ?? items.ToList();

        ReplaceCollection(QueueWaiting, materialized.Where(i => i.Status == Core.Queue.QueuedJobStatus.Queued));
        ReplaceCollection(QueueProcessing, materialized.Where(i => i.Status == Core.Queue.QueuedJobStatus.Processing));
        ReplaceCollection(QueueCompletedToday, materialized
            .Where(i => i.Status == Core.Queue.QueuedJobStatus.Printed && i.Timestamp.Date == today)
            .OrderByDescending(i => i.Timestamp));
        ReplaceCollection(QueueFailedOrRetrying, materialized
            .Where(i => i.Status is Core.Queue.QueuedJobStatus.WaitingRetry or Core.Queue.QueuedJobStatus.FailedPermanently));

        QueueStatusCards[0].Count = QueueWaiting.Count;
        QueueStatusCards[1].Count = QueueProcessing.Count;
        QueueStatusCards[2].Count = QueueCompletedToday.Count;
        QueueStatusCards[3].Count = QueueFailedOrRetrying.Count;
    }

    private static void ReplaceCollection(ObservableCollection<QueueItemViewModel> target, IEnumerable<QueueItemViewModel> items)
    {
        target.Clear();

        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private void OpenQueueDetail(ObservableCollection<QueueItemViewModel> source, string title)
    {
        QueueDetailTitle = title;
        SelectedQueueItems = source;
        IsShowingQueueDetail = true;
    }

    private async void OpenChannelDetail(string channel)
    {
        var detail = new ChannelDetailViewModel(channel) { IsLoading = true };
        SelectedDetail = detail;
        IsShowingDetail = true;

        try
        {
            var orders = await _api.GetChannelOrdersAsync(channel);

            detail.ReplaceOrders(orders.Select(o => new ChannelOrderRowViewModel
            {
                Id = o.Id,
                ExternalOrderId = o.ExternalOrderId,
                CustomerName = o.CustomerName,
                ProductsSummary = string.Join(", ", o.Products.Select(p => p.Quantity > 1 ? $"{p.Name} (x{p.Quantity})" : p.Name)),
                StatusLabel = o.Status,
                GrossAmount = o.GrossAmount,
                FeeAmount = o.FeeAmount,
                NetAmount = o.NetAmount,
                ShippingMethod = o.ShippingMethod,
                CreatedAt = o.CreatedAt,
            }));
        }
        finally
        {
            detail.IsLoading = false;
        }
    }
}
