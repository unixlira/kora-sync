using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using KazakoraAgent.App.Theming;
using KazakoraAgent.Core.Models;
using MahApps.Metro.IconPacks;

namespace KazakoraAgent.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

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
    /// acima toda vez que ReplaceQueueItems roda. Só exibe, não abre nada.
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

    /// Texto diário das Testemunhas de Jeová — null até o primeiro fetch
    /// (DashboardPoller busca já na inicialização, ver Start()) ou se o
    /// servidor ainda não tem nenhuma linha salva. Mostra o versículo (que
    /// já inclui a referência embutida, ver DailyTextFetcherService no
    /// Laravel) — não a data (isso já aparece no relógio à direita) nem o
    /// comentário completo (fonte pequena no meio do cabeçalho, não cabe).
    [ObservableProperty]
    private string? _dailyTextQuote;

    public Visibility HasDailyTextVisibility => DailyTextQuote is null ? Visibility.Collapsed : Visibility.Visible;

    partial void OnDailyTextQuoteChanged(string? value) => OnPropertyChanged(nameof(HasDailyTextVisibility));

    /// Relógio em tempo real do cabeçalho — atualizado a cada segundo por
    /// um DispatcherTimer dedicado em MainWindow.xaml.cs (puramente local,
    /// sem chamada de rede, por isso não faz parte do DashboardPoller).
    [ObservableProperty]
    private string _currentDateTimeText = string.Empty;

    public MainViewModel()
    {
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
            },
            new QueueStatusCardViewModel
            {
                Label = "Imprimindo", Description = "Sendo processado agora mesmo.",
                IconKind = PackIconMaterialKind.Printer, AccentBrush = QueueStatusColors.Processing.Accent,
                CardBackgroundBrush = QueueStatusColors.Processing.SoftBackground,
                CardBorderBrush = QueueStatusColors.Processing.SoftBorder,
            },
            new QueueStatusCardViewModel
            {
                Label = "Concluídas hoje", Description = "Etiquetas impressas com sucesso.",
                IconKind = PackIconMaterialKind.CheckCircleOutline, AccentBrush = QueueStatusColors.Success.Accent,
                CardBackgroundBrush = QueueStatusColors.Success.SoftBackground,
                CardBorderBrush = QueueStatusColors.Success.SoftBorder,
            },
            new QueueStatusCardViewModel
            {
                Label = "Falharam", Description = "Erro ao processar ou aguardando nova tentativa.",
                IconKind = PackIconMaterialKind.AlertCircleOutline, AccentBrush = QueueStatusColors.Error.Accent,
                CardBackgroundBrush = QueueStatusColors.Error.SoftBackground,
                CardBorderBrush = QueueStatusColors.Error.SoftBorder,
            },
        ];
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

    /// Controle de empacotamento: PrintJob.Id de cada etiqueta já conferida
    /// pelo funcionário (botão X) — só existe em memória (some ao reabrir o
    /// KoraSync), nunca é enviado pro servidor. O registro real continua
    /// intacto no banco do Kazakora, isso só filtra o que aparece na tela.
    private readonly HashSet<long> _dismissedLabelJobIds = [];

    /// <summary>A última impressa de verdade fica no topo — LabelDto já vem ordenado assim do servidor (COALESCE(printed_at, created_at) DESC).</summary>
    public void ReplaceLabels(IEnumerable<LabelDto> labels)
    {
        Labels.Clear();

        foreach (var dto in labels)
        {
            if (_dismissedLabelJobIds.Contains(dto.Id))
            {
                continue;
            }

            foreach (var item in LabelItemViewModel.FromDto(dto, DismissLabel))
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

    public void UpdateDailyText(DailyTextDto? dto)
    {
        DailyTextQuote = dto?.ScriptureQuote;
    }

    public void UpdateChannels(IReadOnlyList<ChannelStatusDto> statuses)
    {
        ChannelOrdersCard.UpdateFrom(statuses);
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
}
