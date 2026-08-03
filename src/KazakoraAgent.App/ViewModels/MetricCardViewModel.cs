using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MahApps.Metro.IconPacks;

namespace KazakoraAgent.App.ViewModels;

public partial class MetricCardViewModel : ObservableObject
{
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
}
