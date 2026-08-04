using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MahApps.Metro.IconPacks;

namespace KazakoraAgent.App.ViewModels;

/// <summary>
/// Card de status da fila de impressão — mesmo padrão visual dos cards de
/// "Impressões" do painel Casa Cora (fundo e borda tingidos na cor do
/// status, ícone em bolha, contagem em destaque). Só exibe, não é mais
/// clicável (removido pedido explícito 2026-08-04 — nenhum card do
/// dashboard abre janela/overlay de detalhe).
/// </summary>
public partial class QueueStatusCardViewModel : ObservableObject
{
    public required string Label { get; init; }

    public required string Description { get; init; }

    public required PackIconMaterialKind IconKind { get; init; }

    public required Brush AccentBrush { get; init; }

    public required Brush CardBackgroundBrush { get; init; }

    public required Brush CardBorderBrush { get; init; }

    [ObservableProperty]
    private int _count;
}
