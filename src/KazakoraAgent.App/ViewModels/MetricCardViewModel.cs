using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MahApps.Metro.IconPacks;

namespace KazakoraAgent.App.ViewModels;

public partial class MetricCardViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public required string Label { get; init; }

    public required Brush NumberBrush { get; init; }

    /// Cor do ícone e da bolha (badge) atrás dele — mesma cor, a bolha só
    /// usa opacidade reduzida (ver MetricCardTemplate).
    public required Brush AccentBrush { get; init; }

    /// Ícone da biblioteca Material Design (via MahApps.Metro.IconPacks) —
    /// substitui os paths desenhados à mão que existiam antes.
    public required PackIconMaterialKind IconKind { get; init; }

    [ObservableProperty]
    private string _value = "--";

    /// "Agosto" / "04/08/2026" — nome do mês ou data, vem do servidor
    /// (DashboardMetricsDto.MonthLabel/TodayLabel) pra nunca divergir do
    /// fuso/calendário usado nos cálculos. "--" até a primeira resposta.
    [ObservableProperty]
    private string _periodLabel = "--";

    /// "vs mês anterior" / "vs ontem" — muda por card, texto fixo definido
    /// na criação (ver MainViewModel). Null nos cards que não têm
    /// comparação (ex: Devoluções).
    public string? VariationSuffix { get; init; }

    /// Null quando o servidor não tem base de comparação (ex: mês anterior
    /// sem nenhuma venda) — o card some a linha de variação em vez de
    /// mostrar um 0%/∞ enganoso.
    [ObservableProperty]
    private decimal? _variationPct;

    public string VariationText => VariationPct is { } pct
        ? $"{(pct >= 0 ? "+" : "")}{pct.ToString("0.0", PtBr)}% {VariationSuffix}"
        : string.Empty;

    public Visibility VariationVisibility => VariationPct is null ? Visibility.Collapsed : Visibility.Visible;

    public Brush VariationBrush => VariationPct is >= 0
        ? (Brush) Application.Current.Resources["StatusSuccessBrush"]
        : (Brush) Application.Current.Resources["StatusErrorBrush"];

    partial void OnVariationPctChanged(decimal? value)
    {
        OnPropertyChanged(nameof(VariationText));
        OnPropertyChanged(nameof(VariationVisibility));
        OnPropertyChanged(nameof(VariationBrush));
    }
}
