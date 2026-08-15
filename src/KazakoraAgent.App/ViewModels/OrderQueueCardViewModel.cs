using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KazakoraAgent.App.Theming;
using KazakoraAgent.Core;
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
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// Setado de fora (MainViewModel.PackOrderAsync, ver construtor) — cada
    /// card não fala com a API diretamente, só pede pra quem o alimenta
    /// fazer a chamada e atualizar a fila. Evita todo card carregar uma
    /// referência própria de IKazakoraApiClient.
    public Func<long, Task>? PackRequested { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PackCommand))]
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

    /// Imagem do produto (pedido explícito 2026-08-15) — quem carrega de
    /// verdade é o MainViewModel (fica dono do cache/chamada de rede, ver
    /// MainViewModel.RequestProductImage); este card só expõe onde o
    /// resultado entra e o fallback visual enquanto não tem nada (null =
    /// ainda carregando OU pedido sem imagem, mesmo estado visual pros
    /// dois — não precisa distinguir na tela).
    [ObservableProperty]
    private ImageSource? _productImage;

    public Visibility HasProductImageVisibility => ProductImage is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility NoProductImageVisibility => ProductImage is null ? Visibility.Visible : Visibility.Collapsed;

    partial void OnProductImageChanged(ImageSource? value)
    {
        OnPropertyChanged(nameof(HasProductImageVisibility));
        OnPropertyChanged(nameof(NoProductImageVisibility));
    }

    /// Achado real 2026-08-15: a fila mostra todo pedido do dia, qualquer
    /// status — sem isso, um pedido cancelado (Shopee cancela antes ou
    /// depois de já aparecer aqui) ficava visualmente idêntico a um ativo,
    /// risco real de embalar/despachar algo cancelado. "cancelled" é o
    /// único valor bruto checado (vem de Order::STATUS_CANCELLED no
    /// Laravel) — qualquer outro status mantém a tela normal de sempre.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PackCommand))]
    private string? _status;

    [ObservableProperty]
    private string? _statusLabel;

    public bool IsCancelled => Status == "cancelled";

    public Visibility CancelledBadgeVisibility => IsCancelled ? Visibility.Visible : Visibility.Collapsed;

    /// Botão "Em preparação" some inteiro pra pedido cancelado (não só
    /// desabilita) — nada clicável que pareça uma ação válida a fazer.
    public Visibility PackButtonVisibility => IsCancelled ? Visibility.Collapsed : Visibility.Visible;

    partial void OnStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(IsCancelled));
        OnPropertyChanged(nameof(CancelledBadgeVisibility));
        OnPropertyChanged(nameof(PackButtonVisibility));
    }

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

    /// Data/hora REAL da venda no canal (created_at do pedido no Laravel —
    /// já é o placed_at do marketplace, não a hora que o webhook chegou no
    /// nosso servidor, ver DashboardAgentController::queue()). Pedido
    /// explícito 2026-08-13: exibida ao lado do número do pedido, formato
    /// dd-MM-yyyy HH:mm.
    [ObservableProperty]
    private DateTimeOffset _createdAt;

    public string CreatedAtText => CreatedAt == default
        ? string.Empty
        : CreatedAt.ToLocalTime().ToString("dd-MM-yyyy HH:mm", PtBr);

    partial void OnCreatedAtChanged(DateTimeOffset value) => OnPropertyChanged(nameof(CreatedAtText));

    /// Em voo entre o clique e a resposta do servidor — desabilita o botão
    /// (ver CanPack) e troca o texto pra "Embalando..." (ver MainWindow.xaml)
    /// pra não deixar clicar 2x nem parecer que o clique não fez nada.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PackCommand))]
    private bool _isPacking;

    /// Vem de dto.PackedAt != null (ver UpdateFrom) OU é setado direto
    /// depois de PackAsync ter sucesso (não espera o próximo tick de 2s pra
    /// virar verde). Pedido explícito 2026-08-13 (revisado no mesmo dia):
    /// embalar NÃO tira o pedido da fila — só muda a cor/texto do botão pra
    /// "Embalado", o card continua visível igual antes, o operador usa a
    /// tela como conferência do que já separou.
    [ObservableProperty]
    private bool _isPacked;

    public string PackButtonText => IsPacked ? "Embalado" : (IsPacking ? "Embalando..." : "Em preparação");

    partial void OnIsPackingChanged(bool value) => OnPropertyChanged(nameof(PackButtonText));

    partial void OnIsPackedChanged(bool value) => OnPropertyChanged(nameof(PackButtonText));

    /// Mensagem da última falha ao embalar (rede caiu, servidor fora) — null
    /// quando não há erro, mostrada no ToolTip do botão (ver MainWindow.xaml).
    [ObservableProperty]
    private string? _packErrorMessage;

    public Visibility HasPackErrorVisibility => PackErrorMessage is null ? Visibility.Collapsed : Visibility.Visible;

    partial void OnPackErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasPackErrorVisibility));

    /// Continua clicável mesmo já embalado, de propósito — desabilitar via
    /// IsEnabled=false acionaria o trigger de opacidade 0.4 do estilo padrão
    /// de Button (Controls.xaml), lavando a cor verde que é o ponto inteiro
    /// dessa mudança. Reclicar num pedido já embalado só reconfirma no
    /// servidor (packOrder() é idempotente), inofensivo.
    /// !IsCancelled é defesa em profundidade — o botão já nem aparece
    /// (PackButtonVisibility), isso barra o Command mesmo se algo algum
    /// dia disparar PackAsync por outro caminho (atalho de teclado etc).
    private bool CanPack => HasOrder && !IsPacking && !IsCancelled;

    /// Botão "Em preparação" do card — ao clicar, pede pra quem alimentou
    /// este card (MainViewModel.PackOrderAsync) marcar o pedido como
    /// embalado no servidor. IsPacked vira true assim que a chamada
    /// confirma (otimista-mas-confirmado: só marca depois da resposta OK
    /// do servidor, nunca antes) — não precisa esperar o próximo poll pra
    /// virar verde.
    [RelayCommand(CanExecute = nameof(CanPack))]
    private async Task PackAsync()
    {
        if (PackRequested is null)
        {
            return;
        }

        IsPacking = true;
        PackErrorMessage = null;

        try
        {
            await PackRequested(OrderId);
            IsPacked = true;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Falha ao marcar pedido #{OrderId} como embalado: {ex.Message}");
            PackErrorMessage = "Falha ao embalar — tente de novo.";
        }
        finally
        {
            IsPacking = false;
        }
    }

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
        CreatedAt = default;
        IsPacking = false;
        IsPacked = false;
        PackErrorMessage = null;
        ProductImage = null;
        Status = null;
        StatusLabel = null;
        ProductLines.Clear();
    }

    public void UpdateFrom(OrderQueueItemDto dto)
    {
        HasOrder = true;
        OrderId = dto.Id;
        ExternalOrderId = dto.ExternalOrderId;
        Channel = dto.Channel;
        Status = dto.Status;
        StatusLabel = dto.StatusLabel;
        CustomerName = string.IsNullOrWhiteSpace(dto.CustomerName) ? "Cliente não informado" : dto.CustomerName;
        UnitsCount = dto.UnitsCount;
        CreatedAt = dto.CreatedAt;
        IsPacked = dto.PackedAt is not null;

        // Falha antiga já resolvida não deve continuar aparecendo num
        // pedido que já foi (re)carregado com sucesso da fila.
        PackErrorMessage = null;

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
