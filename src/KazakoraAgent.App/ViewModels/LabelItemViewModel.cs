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

    /// Vocabulário cru do servidor (queued/claimed/printed/failed) — usado
    /// só pra decidir CanPrint, a exibição usa StatusLabel/StatusBrush.
    public required string Status { get; init; }

    public required string StatusLabel { get; init; }

    public required Brush StatusBrush { get; init; }

    public string? ErrorMessage { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    /// Só pendente/com erro pode ser (re)enfileirado manualmente — uma
    /// etiqueta já impressa não tem reimpressão sob demanda ainda (gasta
    /// etiqueta física de verdade, decisão deliberada de não religar isso
    /// sem um endpoint dedicado no servidor).
    public bool CanPrint => Status is "queued" or "claimed" or "failed";

    [ObservableProperty]
    private bool _isPrintRequested;

    public IAsyncRelayCommand PrintCommand { get; }

    public IRelayCommand DetailsCommand { get; }

    public LabelItemViewModel(Func<long, Task<bool>> onPrint, Action<string> onDetails)
    {
        PrintCommand = new AsyncRelayCommand(async () =>
        {
            if (!CanPrint || IsPrintRequested)
            {
                return;
            }

            IsPrintRequested = true;

            try
            {
                await onPrint(JobId);
            }
            finally
            {
                IsPrintRequested = false;
            }
        });

        DetailsCommand = new RelayCommand(() =>
        {
            if (Channel is not null)
            {
                onDetails(Channel);
            }
        });
    }

    public static IEnumerable<LabelItemViewModel> FromDto(LabelDto dto, Func<long, Task<bool>> onPrint, Action<string> onDetails)
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
            yield return new LabelItemViewModel(onPrint, onDetails)
            {
                JobId = dto.Id,
                OrderId = dto.OrderId,
                Channel = dto.Channel,
                ExternalOrderId = dto.ExternalOrderId,
                ProductName = product.Name,
                Quantity = product.Quantity,
                Sku = product.Sku,
                Status = dto.Status,
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
