using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
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
}
