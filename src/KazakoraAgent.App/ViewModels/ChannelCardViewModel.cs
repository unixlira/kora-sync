using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KazakoraAgent.App.Theming;
using KazakoraAgent.Core.Models;

namespace KazakoraAgent.App.ViewModels;

public enum ChannelConnectionStatus
{
    Connected,

    /// Não conseguimos falar com a API agora (sem internet, servidor fora)
    /// — não é a Shopee/ML dizendo "desconectado", é a gente sem conseguir
    /// perguntar. Bolinha amarela, não vermelha.
    Warning,

    Disconnected,
}

public partial class ChannelCardViewModel : ObservableObject
{
    private readonly Action<string> _openDetail;

    public string Channel { get; }

    public string DisplayName => ChannelBrandColors.DisplayNameFor(Channel);

    public Brush AccentBrush => ChannelBrandColors.BrushFor(Channel);

    [ObservableProperty]
    private ChannelConnectionStatus _status = ChannelConnectionStatus.Warning;

    [ObservableProperty]
    private string _lastOrderLabel = "Nenhum pedido ainda";

    [ObservableProperty]
    private string _lastLabelPrintedLabel = "Nenhuma etiqueta ainda";

    [ObservableProperty]
    private int _labelsPrintedToday;

    public IRelayCommand OpenDetailCommand { get; }

    public ChannelCardViewModel(string channel, Action<string> openDetail)
    {
        Channel = channel;
        _openDetail = openDetail;
        OpenDetailCommand = new RelayCommand(() => _openDetail(Channel));
    }

    public Brush StatusBrush => Status switch
    {
        ChannelConnectionStatus.Connected => (Brush) Application.Current.Resources["StatusSuccessBrush"],
        ChannelConnectionStatus.Warning => (Brush) Application.Current.Resources["StatusWarningBrush"],
        _ => (Brush) Application.Current.Resources["StatusErrorBrush"],
    };

    partial void OnStatusChanged(ChannelConnectionStatus value) => OnPropertyChanged(nameof(StatusBrush));

    public void UpdateFrom(ChannelStatusDto dto)
    {
        Status = dto.Connected ? ChannelConnectionStatus.Connected : ChannelConnectionStatus.Disconnected;

        LastOrderLabel = dto.LastOrder is null
            ? "Nenhum pedido ainda"
            : $"#{dto.LastOrder.Id} — {dto.LastOrder.CreatedAt.LocalDateTime:dd/MM HH:mm}";

        LastLabelPrintedLabel = dto.LastLabelPrintedAt is null
            ? "Nenhuma etiqueta ainda"
            : dto.LastLabelPrintedAt.Value.LocalDateTime.ToString("dd/MM HH:mm");

        LabelsPrintedToday = dto.LabelsPrintedToday;
    }

    /// Chamado quando o poll pra API falhou inteiro (não deu nem pra saber
    /// o status real) — mantém os últimos dados conhecidos na tela, só
    /// sinaliza a incerteza na bolinha.
    public void MarkUnreachable() => Status = ChannelConnectionStatus.Warning;
}
