using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using KazakoraAgent.App.Theming;
using KazakoraAgent.Core.Models;

namespace KazakoraAgent.App.ViewModels;

/// <summary>
/// Card de pedido na fila de expedição — usado nos 2 cards em destaque
/// (grandes, com Viewbox pra fonte crescer/encolher e nunca cortar, ver
/// MainWindow.xaml) e reaproveitado na lista compacta do resto do dia
/// (mesmos dados, template menor, sem Viewbox). Pedido explícito
/// 2026-08-06: número do pedido + QTD + TODOS os produtos (não só o
/// primeiro — é exatamente um pedido de 2 itens, só 1 percebido, que
/// causou a devolução que motivou o painel de expedição original, ver
/// commit 6cfa401 do Kazakora) + cliente + canal + id externo.
/// </summary>
public partial class OrderQueueCardViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _hasOrder;

    public Visibility HasOrderVisibility => HasOrder ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility => HasOrder ? Visibility.Collapsed : Visibility.Visible;

    partial void OnHasOrderChanged(bool value)
    {
        OnPropertyChanged(nameof(HasOrderVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
    }

    [ObservableProperty]
    private long _orderId;

    public string OrderNumberText => $"#{OrderId}";

    [ObservableProperty]
    private string? _externalOrderId;

    [ObservableProperty]
    private string? _channel;

    public string ChannelDisplayName => Channel is null ? "—" : ChannelBrandColors.DisplayNameFor(Channel);

    public Brush ChannelAccentBrush => Channel is null ? System.Windows.Media.Brushes.Gray : ChannelBrandColors.BrushFor(Channel);

    [ObservableProperty]
    private string _customerName = string.Empty;

    [ObservableProperty]
    private int _unitsCount;

    /// "{qty}x {nome}" por produto — TODOS, nunca só o primeiro (ver
    /// comentário da classe). ItemsControl dentro de um Viewbox no card
    /// grande encolhe a fonte junto quando há mais de um produto, pra caber
    /// sem cortar (ver MainWindow.xaml).
    public ObservableCollection<string> ProductLines { get; } = [];

    partial void OnChannelChanged(string? value)
    {
        OnPropertyChanged(nameof(ChannelDisplayName));
        OnPropertyChanged(nameof(ChannelAccentBrush));
    }

    partial void OnOrderIdChanged(long value) => OnPropertyChanged(nameof(OrderNumberText));

    public void Clear()
    {
        HasOrder = false;
        OrderId = 0;
        ExternalOrderId = null;
        Channel = null;
        CustomerName = string.Empty;
        UnitsCount = 0;
        ProductLines.Clear();
    }

    public void UpdateFrom(OrderQueueItemDto dto)
    {
        HasOrder = true;
        OrderId = dto.Id;
        ExternalOrderId = dto.ExternalOrderId;
        Channel = dto.Channel;
        CustomerName = string.IsNullOrWhiteSpace(dto.CustomerName) ? "Cliente não informado" : dto.CustomerName;
        UnitsCount = dto.UnitsCount;

        ProductLines.Clear();
        foreach (var product in dto.Products)
        {
            var line = product.Quantity > 1 ? $"{product.Quantity}x {product.Name}" : product.Name;

            if (!string.IsNullOrWhiteSpace(product.Sku))
            {
                line += $" (SKU: {product.Sku})";
            }

            ProductLines.Add(line);
        }

        if (ProductLines.Count == 0)
        {
            ProductLines.Add("Produto não identificado");
        }
    }
}
