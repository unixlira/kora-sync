using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using KazakoraAgent.App.Theming;

namespace KazakoraAgent.App.ViewModels;

public partial class ChannelDetailViewModel : ObservableObject
{
    public string Channel { get; }

    public string DisplayName => ChannelBrandColors.DisplayNameFor(Channel);

    public ObservableCollection<ChannelOrderRowViewModel> Orders { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    public ChannelDetailViewModel(string channel)
    {
        Channel = channel;
    }

    public void ReplaceOrders(IEnumerable<ChannelOrderRowViewModel> orders)
    {
        Orders.Clear();

        foreach (var order in orders)
        {
            Orders.Add(order);
        }
    }
}
