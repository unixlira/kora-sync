using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.IconPacks;

namespace KazakoraAgent.App.ViewModels;

/// <summary>
/// Card de status da fila de impressão — mesmo padrão visual dos cards de
/// "Impressões" do painel Casa Cora (fundo e borda tingidos na cor do
/// status, ícone em bolha, contagem em destaque). Clicar abre a listagem
/// completa daquele status (ver MainViewModel.OpenQueueDetail).
/// </summary>
public partial class QueueStatusCardViewModel : ObservableObject
{
    public required string Label { get; init; }

    public required string Description { get; init; }

    public required PackIconMaterialKind IconKind { get; init; }

    public required Brush AccentBrush { get; init; }

    public required Brush CardBackgroundBrush { get; init; }

    public required Brush CardBorderBrush { get; init; }

    public required IRelayCommand OpenCommand { get; init; }

    [ObservableProperty]
    private int _count;
}
