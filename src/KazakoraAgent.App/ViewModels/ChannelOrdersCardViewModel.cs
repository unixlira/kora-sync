using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using KazakoraAgent.App.Theming;
using KazakoraAgent.Core.Models;
using MahApps.Metro.IconPacks;

namespace KazakoraAgent.App.ViewModels;

/// <summary>
/// Card do topo "Pedidos por canal" — uma badge "Nome: total" por
/// integração de marketplace, sempre as 5 (pedido explícito 2026-08-04:
/// mostrar todas mesmo zerada, não só quem vendeu hoje) — a própria loja
/// (site) fica de fora, esse card é especificamente sobre os canais
/// externos. Visualmente é o mesmo card de métrica dos outros 4 do topo,
/// só que o "valor" é essa lista em vez de um número único — ver
/// DataTemplate próprio (ChannelOrdersCardTemplate).
/// </summary>
public partial class ChannelOrdersCardViewModel : ObservableObject
{
    public string Label => "Pedidos por canal — hoje";

    public PackIconMaterialKind IconKind => PackIconMaterialKind.ChartBoxOutline;

    public ObservableCollection<ChannelOrderCountViewModel> Entries { get; } = [];

    public void UpdateFrom(IReadOnlyList<ChannelStatusDto> statuses)
    {
        var byChannel = statuses.ToDictionary(s => s.Channel);

        var ordered = MarketplaceChannel.All
            .Where(channel => channel != MarketplaceChannel.Store)
            .Select(channel => new ChannelOrderCountViewModel
            {
                DisplayName = ChannelBrandColors.ShortDisplayNameFor(channel),
                AccentBrush = ChannelBrandColors.BrushFor(channel),
                // Fundo amarelo do Mercado Livre é claro demais pra texto
                // branco ficar legível — só essa badge usa texto preto,
                // pedido explícito 2026-08-04.
                TextBrush = channel == MarketplaceChannel.MercadoLivre ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.White,
                Count = byChannel.TryGetValue(channel, out var dto) ? dto.OrdersToday : 0,
            })
            .OrderByDescending(entry => entry.Count);

        Entries.Clear();

        foreach (var entry in ordered)
        {
            Entries.Add(entry);
        }
    }
}

public sealed class ChannelOrderCountViewModel
{
    public required string DisplayName { get; init; }

    public required Brush AccentBrush { get; init; }

    public required Brush TextBrush { get; init; }

    public required int Count { get; init; }

    public string Text => $"{DisplayName}: {Count}";
}
