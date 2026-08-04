using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using KazakoraAgent.App.Theming;
using KazakoraAgent.Core.Models;
using MahApps.Metro.IconPacks;

namespace KazakoraAgent.App.ViewModels;

/// <summary>
/// Card do topo "Pedidos por canal" — uma linha "Nome: total" por canal,
/// só pros canais com pelo menos 1 pedido hoje (sem inflar o card com
/// canais zerados/desconectados). Visualmente é o mesmo card de métrica
/// dos outros 4 do topo, só que o "valor" é essa lista em vez de um
/// número único — ver DataTemplate próprio (ChannelOrdersCardTemplate).
/// </summary>
public partial class ChannelOrdersCardViewModel : ObservableObject
{
    public string Label => "Pedidos por canal — hoje";

    public PackIconMaterialKind IconKind => PackIconMaterialKind.ChartBoxOutline;

    public ObservableCollection<ChannelOrderCountViewModel> Entries { get; } = [];

    [ObservableProperty]
    private bool _hasNoOrdersToday = true;

    public void UpdateFrom(IReadOnlyList<ChannelStatusDto> statuses)
    {
        Entries.Clear();

        foreach (var status in statuses.Where(s => s.OrdersToday > 0).OrderByDescending(s => s.OrdersToday))
        {
            Entries.Add(new ChannelOrderCountViewModel
            {
                DisplayName = ChannelBrandColors.DisplayNameFor(status.Channel),
                AccentBrush = ChannelBrandColors.BrushFor(status.Channel),
                Count = status.OrdersToday,
            });
        }

        HasNoOrdersToday = Entries.Count == 0;
    }
}

public sealed class ChannelOrderCountViewModel
{
    public required string DisplayName { get; init; }

    public required System.Windows.Media.Brush AccentBrush { get; init; }

    public required int Count { get; init; }

    public string Text => $"{DisplayName}: {Count}";
}
