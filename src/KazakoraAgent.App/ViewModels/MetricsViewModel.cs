using CommunityToolkit.Mvvm.ComponentModel;
using KazakoraAgent.Core.Models;

namespace KazakoraAgent.App.ViewModels;

public partial class MetricsViewModel : ObservableObject
{
    [ObservableProperty]
    private decimal _revenueToday;

    [ObservableProperty]
    private int _salesToday;

    [ObservableProperty]
    private int _cancelledToday;

    [ObservableProperty]
    private int _refundedToday;

    [ObservableProperty]
    private int _cartItemsCount;

    public void UpdateFrom(DashboardMetricsDto dto)
    {
        RevenueToday = dto.RevenueToday;
        SalesToday = dto.SalesToday;
        CancelledToday = dto.CancelledToday;
        RefundedToday = dto.RefundedToday;
        CartItemsCount = dto.CartItemsCount;
    }
}
