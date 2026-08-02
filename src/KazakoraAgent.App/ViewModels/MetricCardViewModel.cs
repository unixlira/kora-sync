using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KazakoraAgent.App.ViewModels;

public partial class MetricCardViewModel : ObservableObject
{
    public required string Label { get; init; }

    public required Brush NumberBrush { get; init; }

    [ObservableProperty]
    private string _value = "--";
}
