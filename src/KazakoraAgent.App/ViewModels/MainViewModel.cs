using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KazakoraAgent.App.Theming;
using KazakoraAgent.Core.Api;
using KazakoraAgent.Core.Models;

namespace KazakoraAgent.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IKazakoraApiClient _api;

    public MetricsViewModel Metrics { get; } = new();

    public ObservableCollection<ChannelCardViewModel> Channels { get; }

    /// Um card por métrica do topo — mantido em sincronia com Metrics
    /// sempre que UpdateMetrics roda (ver DashboardTickAsync no poller).
    public ObservableCollection<MetricCardViewModel> MetricCards { get; }

    public ObservableCollection<QueueItemViewModel> QueueWaiting { get; } = [];

    public ObservableCollection<QueueItemViewModel> QueueProcessing { get; } = [];

    public ObservableCollection<QueueItemViewModel> QueueCompletedToday { get; } = [];

    public ObservableCollection<QueueItemViewModel> QueueFailedOrRetrying { get; } = [];

    [ObservableProperty]
    private bool _isShowingDetail;

    [ObservableProperty]
    private ChannelDetailViewModel? _selectedDetail;

    public IRelayCommand CloseDetailCommand { get; }

    public MainViewModel(IKazakoraApiClient api)
    {
        _api = api;

        Channels = new ObservableCollection<ChannelCardViewModel>(
            MarketplaceChannel.All.Select(channel => new ChannelCardViewModel(channel, OpenChannelDetail)));

        var resources = Application.Current.Resources;

        var brandBrush = (Brush) resources["BrandPrimaryBrush"];
        var processingBrush = (Brush) resources["StatusProcessingBrush"];
        var errorBrush = (Brush) resources["StatusErrorBrush"];
        var warningBrush = (Brush) resources["StatusWarningBrush"];
        var neutralBrush = (Brush) resources["TextPrimaryBrush"];

        MetricCards =
        [
            new MetricCardViewModel { Label = "Faturamento", NumberBrush = neutralBrush, AccentBrush = brandBrush, Icon = MetricIcons.Revenue },
            new MetricCardViewModel { Label = "Pedidos", NumberBrush = neutralBrush, AccentBrush = processingBrush, Icon = MetricIcons.Orders },
            new MetricCardViewModel { Label = "Cancelados", NumberBrush = neutralBrush, AccentBrush = errorBrush, Icon = MetricIcons.Cancelled },
            new MetricCardViewModel { Label = "Reembolsos", NumberBrush = neutralBrush, AccentBrush = warningBrush, Icon = MetricIcons.Refunds },
            new MetricCardViewModel { Label = "Carrinho", NumberBrush = neutralBrush, AccentBrush = neutralBrush, Icon = MetricIcons.Cart },
        ];

        CloseDetailCommand = new RelayCommand(() =>
        {
            IsShowingDetail = false;
            SelectedDetail = null;
        });
    }

    public void UpdateMetrics(DashboardMetricsDto dto)
    {
        Metrics.UpdateFrom(dto);

        MetricCards[0].Value = dto.RevenueToday.ToString("C2", PtBr);
        MetricCards[1].Value = dto.SalesToday.ToString(PtBr);
        MetricCards[2].Value = dto.CancelledToday.ToString(PtBr);
        MetricCards[3].Value = dto.RefundedToday.ToString(PtBr);
        MetricCards[4].Value = dto.CartItemsCount.ToString(PtBr);
    }

    public void UpdateChannels(IReadOnlyList<ChannelStatusDto> statuses)
    {
        var byChannel = statuses.ToDictionary(s => s.Channel);

        foreach (var card in Channels)
        {
            if (byChannel.TryGetValue(card.Channel, out var dto))
            {
                card.UpdateFrom(dto);
            }
            else
            {
                card.MarkUnreachable();
            }
        }
    }

    public void MarkChannelsUnreachable()
    {
        foreach (var card in Channels)
        {
            card.MarkUnreachable();
        }
    }

    public void ReplaceQueueItems(IEnumerable<QueueItemViewModel> items)
    {
        var today = DateTimeOffset.Now.Date;

        ReplaceCollection(QueueWaiting, items.Where(i => i.Status == Core.Queue.QueuedJobStatus.Queued));
        ReplaceCollection(QueueProcessing, items.Where(i => i.Status == Core.Queue.QueuedJobStatus.Processing));
        ReplaceCollection(QueueCompletedToday, items
            .Where(i => i.Status == Core.Queue.QueuedJobStatus.Printed && i.Timestamp.Date == today)
            .OrderByDescending(i => i.Timestamp));
        ReplaceCollection(QueueFailedOrRetrying, items
            .Where(i => i.Status is Core.Queue.QueuedJobStatus.WaitingRetry or Core.Queue.QueuedJobStatus.FailedPermanently));
    }

    private static void ReplaceCollection(ObservableCollection<QueueItemViewModel> target, IEnumerable<QueueItemViewModel> items)
    {
        target.Clear();

        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private async void OpenChannelDetail(string channel)
    {
        var detail = new ChannelDetailViewModel(channel) { IsLoading = true };
        SelectedDetail = detail;
        IsShowingDetail = true;

        try
        {
            var orders = await _api.GetChannelOrdersAsync(channel);

            detail.ReplaceOrders(orders.Select(o => new ChannelOrderRowViewModel
            {
                Id = o.Id,
                ExternalOrderId = o.ExternalOrderId,
                CustomerName = o.CustomerName,
                ProductsSummary = string.Join(", ", o.Products.Select(p => p.Quantity > 1 ? $"{p.Name} (x{p.Quantity})" : p.Name)),
                StatusLabel = o.Status,
                GrossAmount = o.GrossAmount,
                FeeAmount = o.FeeAmount,
                NetAmount = o.NetAmount,
                ShippingMethod = o.ShippingMethod,
                CreatedAt = o.CreatedAt,
            }));
        }
        finally
        {
            detail.IsLoading = false;
        }
    }
}
