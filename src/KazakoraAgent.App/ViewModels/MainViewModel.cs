using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using KazakoraAgent.Core;
using KazakoraAgent.Core.Api;
using KazakoraAgent.Core.Models;
using MahApps.Metro.IconPacks;

namespace KazakoraAgent.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// Null só em cenário de design-time/teste sem API de verdade (ver
    /// construtor sem argumento) — o botão "Em preparação" de cada card
    /// (OrderQueueCardViewModel.PackRequested) simplesmente não faz nada
    /// nesse caso, em vez de estourar NullReferenceException.
    private readonly IKazakoraApiClient? _api;

    /// Um card por métrica do topo — mantido em sincronia com Metrics
    /// sempre que UpdateMetrics roda (ver DashboardTickAsync no poller).
    public ObservableCollection<MetricCardViewModel> MetricCards { get; }

    /// 5º card do topo — "Pedidos por canal", atualizado junto com
    /// UpdateChannels (mesmo tick, mesma fonte de dados).
    public ChannelOrdersCardViewModel ChannelOrdersCard { get; } = new();

    /// União ordenada dos 4 MetricCardViewModel + o ChannelOrdersCard, na
    /// ordem exata em que devem aparecer na UniformGrid do topo — a XAML
    /// usa DataTemplate implícito por tipo (sem ItemTemplate fixo) pra
    /// renderizar os dois tipos misturados na mesma linha.
    public ObservableCollection<object> TopCards { get; }

    /// Os 3 cards em destaque da fila de expedição de hoje — pedido mais
    /// recente (QueueCard1), segundo mais recente (QueueCard2) e terceiro
    /// (QueueCard3, pedido explícito 2026-08-15 — coube depois do
    /// redesenho compacto do card, ver MainWindow.xaml). Instâncias fixas
    /// (não uma ObservableCollection) porque a XAML liga cada uma direto
    /// num Border próprio, não via ItemsControl — mais simples de
    /// posicionar 3 "quadrados" fixos em coluna. Ver UpdateOrderQueue.
    public OrderQueueCardViewModel QueueCard1 { get; } = new();

    public OrderQueueCardViewModel QueueCard2 { get; } = new();

    public OrderQueueCardViewModel QueueCard3 { get; } = new();

    /// 4º pedido do dia em diante, mesma ordem decrescente — lista com
    /// scroll próprio (coluna da direita, ver MainWindow.xaml).
    public ObservableCollection<OrderQueueCardViewModel> QueueRest { get; } = [];

    /// Cache de imagem por pedido (pedido explícito 2026-08-15) — evita
    /// baixar de novo a cada tick de 2s (DashboardTickAsync) o mesmo pedido
    /// que já está na fila há um tempo. Guarda também o "não tem imagem"
    /// (null) como resultado válido, pra não bater no endpoint de novo a
    /// cada tick só pra levar 404 de novo — só reseta ao reabrir o app.
    private readonly Dictionary<long, ImageSource?> _imageCache = new();

    /// Mensagem da última falha ao buscar canais/métricas — null quando o
    /// último tick foi bem-sucedido. Ver DashboardPoller.DashboardTickAsync.
    [ObservableProperty]
    private string? _lastDashboardError;

    public Visibility HasDashboardErrorVisibility => LastDashboardError is null ? Visibility.Collapsed : Visibility.Visible;

    partial void OnLastDashboardErrorChanged(string? value) => OnPropertyChanged(nameof(HasDashboardErrorVisibility));

    /// Texto diário das Testemunhas de Jeová — null até o primeiro fetch
    /// (DashboardPoller busca já na inicialização, ver Start()) ou se o
    /// servidor ainda não tem nenhuma linha salva. Mostra o versículo em
    /// si (DailyTextQuote) com a referência bíblica sempre na linha de
    /// baixo (DailyTextReference, pedido explícito 2026-08-04 — os dois
    /// já vêm separados do servidor, ver DailyTextFetcherService no
    /// Laravel) — não a data (isso já aparece no relógio à direita) nem o
    /// comentário completo (fonte pequena no meio do cabeçalho, não cabe).
    [ObservableProperty]
    private string? _dailyTextQuote;

    [ObservableProperty]
    private string? _dailyTextReference;

    public Visibility HasDailyTextVisibility => DailyTextQuote is null ? Visibility.Collapsed : Visibility.Visible;

    partial void OnDailyTextQuoteChanged(string? value) => OnPropertyChanged(nameof(HasDailyTextVisibility));

    /// Relógio em tempo real do cabeçalho — atualizado a cada segundo por
    /// um DispatcherTimer dedicado em MainWindow.xaml.cs (puramente local,
    /// sem chamada de rede, por isso não faz parte do DashboardPoller).
    [ObservableProperty]
    private string _currentDateTimeText = string.Empty;

    public MainViewModel(IKazakoraApiClient? api = null)
    {
        _api = api;

        var resources = Application.Current.Resources;

        var successBrush = (Brush) resources["StatusSuccessBrush"];
        var processingBrush = (Brush) resources["StatusProcessingBrush"];
        var purpleBrush = new SolidColorBrush((Color) ColorConverter.ConvertFromString("#7B61FF")!);
        var errorBrush = (Brush) resources["StatusErrorBrush"];
        var neutralBrush = (Brush) resources["TextPrimaryBrush"];

        // 4 cards fixos (visão geral, todas as plataformas somadas) — ver
        // UpdateMetrics pro preenchimento. Índices usados lá são
        // posicionais de propósito (mesmo padrão já existente antes desta
        // mudança), então a ordem aqui importa.
        MetricCards =
        [
            new MetricCardViewModel { Label = "Faturamento do mês", NumberBrush = neutralBrush, AccentBrush = successBrush, IconKind = PackIconMaterialKind.TrendingUp, VariationSuffix = "vs mês anterior" },
            new MetricCardViewModel { Label = "Faturamento de hoje", NumberBrush = neutralBrush, AccentBrush = processingBrush, IconKind = PackIconMaterialKind.CalendarClock, VariationSuffix = "vs ontem" },
            new MetricCardViewModel { Label = "Pedidos hoje", NumberBrush = neutralBrush, AccentBrush = purpleBrush, IconKind = PackIconMaterialKind.PackageVariantClosed, VariationSuffix = "vs ontem" },
            new MetricCardViewModel { Label = "Devoluções do mês", NumberBrush = neutralBrush, AccentBrush = errorBrush, IconKind = PackIconMaterialKind.KeyboardReturn },
        ];

        TopCards = [.. MetricCards, ChannelOrdersCard];

        // Os 2 cards fixos precisam do callback já na construção — os da
        // QueueRest recebem o mesmo callback quando são criados, ver
        // UpdateOrderQueue.
        QueueCard1.PackRequested = PackOrderAsync;
        QueueCard2.PackRequested = PackOrderAsync;
        QueueCard3.PackRequested = PackOrderAsync;
    }

    public void UpdateMetrics(DashboardMetricsDto dto)
    {
        MetricCards[0].Value = dto.RevenueMonth.ToString("C2", PtBr);
        MetricCards[0].PeriodLabel = dto.MonthLabel;
        MetricCards[0].VariationPct = dto.RevenueMonthVariationPct;

        MetricCards[1].Value = dto.RevenueToday.ToString("C2", PtBr);
        MetricCards[1].PeriodLabel = dto.TodayLabel;
        MetricCards[1].VariationPct = dto.RevenueTodayVariationPct;

        MetricCards[2].Value = dto.SalesToday.ToString(PtBr);
        MetricCards[2].PeriodLabel = dto.TodayLabel;
        MetricCards[2].VariationPct = dto.SalesTodayVariationPct;

        MetricCards[3].Value = dto.ReturnsMonth.ToString(PtBr);
        MetricCards[3].PeriodLabel = dto.MonthLabel;
    }

    public void UpdateDailyText(DailyTextDto? dto)
    {
        DailyTextQuote = dto?.ScriptureQuote;
        DailyTextReference = dto?.ScriptureReference;
    }

    public void UpdateChannels(IReadOnlyList<ChannelStatusDto> statuses)
    {
        ChannelOrdersCard.UpdateFrom(statuses);
    }

    /// <summary>
    /// items já vem do servidor em ordem decrescente (ver
    /// DashboardAgentController::queue, orderByDesc('id')). QueueCard1/2/3
    /// são reaproveitados em vez de recriados a cada tick — só os campos
    /// mudam — pra não perder o estado de scroll/seleção de nada que
    /// dependa de identidade de objeto no futuro; QueueRest é reconstruída
    /// (mais simples, e a lista muda de tamanho a cada pedido novo/enviado
    /// de qualquer forma).
    /// </summary>
    public void UpdateOrderQueue(IReadOnlyList<OrderQueueItemDto> items)
    {
        if (items.Count > 0)
        {
            QueueCard1.UpdateFrom(items[0]);
            RequestProductImage(QueueCard1, items[0].Id);
        }
        else
        {
            QueueCard1.Clear();
        }

        if (items.Count > 1)
        {
            QueueCard2.UpdateFrom(items[1]);
            RequestProductImage(QueueCard2, items[1].Id);
        }
        else
        {
            QueueCard2.Clear();
        }

        if (items.Count > 2)
        {
            QueueCard3.UpdateFrom(items[2]);
            RequestProductImage(QueueCard3, items[2].Id);
        }
        else
        {
            QueueCard3.Clear();
        }

        QueueRest.Clear();
        foreach (var dto in items.Skip(3))
        {
            var item = new OrderQueueCardViewModel { PackRequested = PackOrderAsync };
            item.UpdateFrom(dto);
            QueueRest.Add(item);
            RequestProductImage(item, dto.Id);
        }
    }

    /// Preenche ProductImage do card a partir do cache (síncrono — evita
    /// mostrar por um instante a imagem do pedido ANTERIOR quando
    /// QueueCard1/QueueCard2 são reaproveitados pra um pedido novo, já que
    /// isso roda logo depois de UpdateFrom, no mesmo tick) ou, se ainda não
    /// tem no cache, limpa e dispara a busca em segundo plano.
    private void RequestProductImage(OrderQueueCardViewModel card, long orderId)
    {
        if (_imageCache.TryGetValue(orderId, out var cached))
        {
            card.ProductImage = cached;
            return;
        }

        card.ProductImage = null;
        _ = FetchProductImageAsync(card, orderId);
    }

    private async Task FetchProductImageAsync(OrderQueueCardViewModel card, long orderId)
    {
        if (_api is null)
        {
            return;
        }

        ImageSource? image = null;

        try
        {
            var bytes = await _api.DownloadOrderImageAsync(orderId);
            if (bytes is not null)
            {
                image = DecodeImage(bytes);
            }

            // Só entra no cache em caso de sucesso (com ou sem imagem) —
            // uma falha de rede no meio do caminho não deve "travar" esse
            // pedido como sem imagem pra sempre; o próximo tick tenta de
            // novo.
            _imageCache[orderId] = image;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Falha ao baixar imagem do pedido #{orderId}: {ex.Message}");
            return;
        }

        // O card pode ter sido reaproveitado (QueueCard1/QueueCard2) pra um
        // pedido diferente enquanto esse download estava em voo — só aplica
        // o resultado se ainda for do mesmo pedido.
        if (card.OrderId == orderId)
        {
            card.ProductImage = image;
        }
    }

    private static ImageSource DecodeImage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// Callback por trás do botão "Em preparação" de todo card da fila
    /// (pedido explícito 2026-08-13) — chamado por
    /// OrderQueueCardViewModel.PackCommand. Só confirma no servidor; o
    /// próprio card vira "Embalado" sozinho assim que este await termina
    /// sem erro (ver OrderQueueCardViewModel.PackAsync) — diferente da
    /// primeira versão deste botão, o pedido NÃO sai da fila, então não há
    /// motivo pra buscar a lista inteira de novo aqui.
    private Task PackOrderAsync(long orderId) => _api?.PackOrderAsync(orderId) ?? Task.CompletedTask;
}
