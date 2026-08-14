using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using KazakoraAgent.App.Theming;
using KazakoraAgent.Core.Models;

namespace KazakoraAgent.App.ViewModels;

/// <summary>
/// Card de venda AGENDADA pelo canal (pedido explícito 2026-08-14, achado
/// no pedido #278 — Coleta/Places do Mercado Livre com etiqueta liberada
/// só perto de uma data futura, não um pedido travado de verdade). Só
/// exibição — sem comando/botão, diferente de OrderQueueCardViewModel:
/// aqui não tem nada pra "fazer", é só pra time saber que aquele pedido
/// está normal, só esperando o canal.
/// </summary>
public sealed class ScheduledShipmentCardViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public long OrderId { get; init; }

    public string OrderNumberText => $"#{OrderId}";

    public string? ExternalOrderId { get; init; }

    public string? Channel { get; init; }

    public string ChannelDisplayName => Channel is null ? "—" : ChannelBrandColors.DisplayNameFor(Channel);

    public Brush ChannelAccentBrush => Channel is null ? System.Windows.Media.Brushes.Gray : ChannelBrandColors.BrushFor(Channel);

    public string CustomerName { get; init; } = "Cliente não informado";

    public DateTimeOffset ScheduledFor { get; init; }

    public string ScheduledForText => ScheduledFor.ToLocalTime().ToString("dd-MM-yyyy", PtBr);

    public bool IsOverdue { get; init; }

    /// Texto pronto pra tela — muda de "aviso tranquilo" pra "atenção" só
    /// pela cor (ver MainWindow.xaml), o texto em si já deixa claro os dois
    /// casos sem precisar de ícone extra.
    public string StatusText => IsOverdue
        ? $"Devia ter liberado em {ScheduledForText} — ainda não liberou"
        : $"Etiqueta só sai perto de {ScheduledForText}";

    public ObservableCollection<string> ProductLines { get; } = [];

    public static ScheduledShipmentCardViewModel FromDto(ScheduledShipmentDto dto)
    {
        var card = new ScheduledShipmentCardViewModel
        {
            OrderId = dto.OrderId,
            ExternalOrderId = dto.ExternalOrderId,
            Channel = dto.Channel,
            CustomerName = string.IsNullOrWhiteSpace(dto.CustomerName) ? "Cliente não informado" : dto.CustomerName,
            ScheduledFor = dto.ScheduledFor,
            IsOverdue = dto.IsOverdue,
        };

        foreach (var product in dto.Products)
        {
            card.ProductLines.Add(product.Quantity > 1 ? $"{product.Quantity}x {product.Name}" : product.Name);
        }

        if (card.ProductLines.Count == 0)
        {
            card.ProductLines.Add("Produto não identificado");
        }

        return card;
    }
}
