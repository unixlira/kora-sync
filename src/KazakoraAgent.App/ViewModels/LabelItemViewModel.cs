using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KazakoraAgent.App.Theming;
using KazakoraAgent.Core.Models;

namespace KazakoraAgent.App.ViewModels;

/// <summary>
/// Uma linha por produto (não por PrintJob) — um pedido com 2 produtos vira
/// 2 cards na lista, cada um repetindo marketplace/pedido/status, pra bater
/// com o pedido literal de "quantidade + nome do produto + SKU" por card.
/// Lista de checagem pra empacotamento — não dispara nada, só informa (a
/// impressão em si já é automática, ver QueueEngine/DashboardPoller).
/// </summary>
public partial class LabelItemViewModel : ObservableObject
{
    public required long JobId { get; init; }

    public long? OrderId { get; init; }

    public string? Channel { get; init; }

    public string? ChannelDisplayName => Channel is null ? "Canal desconhecido" : ChannelBrandColors.DisplayNameFor(Channel);

    public Brush ChannelAccentBrush => Channel is null ? System.Windows.Media.Brushes.Gray : ChannelBrandColors.BrushFor(Channel);

    public string? ExternalOrderId { get; init; }

    public required string ProductName { get; init; }

    public required int Quantity { get; init; }

    public string QuantityLabel => $"{Quantity}x";

    public string? Sku { get; init; }

    public required string StatusLabel { get; init; }

    public required Brush StatusBrush { get; init; }

    public string? ErrorMessage { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    /// Controle de empacotamento (pedido explícito 2026-08-04): funcionário
    /// bateu o olho, empacotou o produto certo na quantidade certa, marca
    /// como resolvido — some da lista, mas o PrintJob no servidor continua
    /// intacto (isso é só um "já vi" local, ver MainViewModel.DismissLabel).
    public IRelayCommand DismissCommand { get; }

    public LabelItemViewModel(Action<long> onDismiss)
    {
        DismissCommand = new RelayCommand(() => onDismiss(JobId));
    }

    public static IEnumerable<LabelItemViewModel> FromDto(LabelDto dto, Action<long> onDismiss)
    {
        var (label, brush) = Describe(dto.Status);

        // Job manual/simulado sem item mapeado (ver LabelProcessingService
        // no Laravel) — ainda mostra uma linha, só sem nome/SKU reais, em
        // vez de sumir da lista silenciosamente.
        var products = dto.Products.Count > 0
            ? dto.Products
            : [new LabelProductDto { Name = "Produto não identificado", Quantity = 0 }];

        foreach (var product in products)
        {
            yield return new LabelItemViewModel(onDismiss)
            {
                JobId = dto.Id,
                OrderId = dto.OrderId,
                Channel = dto.Channel,
                ExternalOrderId = dto.ExternalOrderId,
                ProductName = product.Name,
                Quantity = product.Quantity,
                Sku = product.Sku,
                StatusLabel = label,
                StatusBrush = brush,
                ErrorMessage = dto.ErrorMessage,
                Timestamp = dto.PrintedAt ?? dto.CreatedAt,
            };
        }
    }

    private static (string Label, Brush Brush) Describe(string status)
    {
        var resources = Application.Current.Resources;

        return status switch
        {
            "queued" => ("Pendente de impressão", (Brush) resources["StatusProcessingBrush"]),
            "claimed" => ("Imprimindo", (Brush) resources["StatusProcessingBrush"]),
            "printed" => ("Impressa", (Brush) resources["StatusSuccessBrush"]),
            "failed" => ("Erro", (Brush) resources["StatusErrorBrush"]),
            _ => (status, (Brush) resources["StatusProcessingBrush"]),
        };
    }
}
