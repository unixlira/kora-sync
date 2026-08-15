using System.Text.Json.Serialization;

namespace KazakoraAgent.Core.Models;

/// <summary>
/// Espelha um item de GET /api/print-agent/dashboard/queue
/// (DashboardAgentController::queue no Laravel) — pedido pago do dia, ainda
/// não embalado/enviado. Mesmo conceito da fila já usada na versão web
/// (Modules\Admin\Http\Controllers\PrintJobController::index()), mas
/// filtrado a mais pra só hoje (pedido explícito 2026-08-06, exclusivo
/// desse fluxo nativo).
/// </summary>
public sealed class OrderQueueItemDto
{
    [JsonPropertyName("id")]
    public required long Id { get; init; }

    [JsonPropertyName("external_order_id")]
    public string? ExternalOrderId { get; init; }

    [JsonPropertyName("channel")]
    public string? Channel { get; init; }

    [JsonPropertyName("customer_name")]
    public string? CustomerName { get; init; }

    [JsonPropertyName("units_count")]
    public required int UnitsCount { get; init; }

    [JsonPropertyName("products")]
    public required List<OrderQueueProductDto> Products { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// Null enquanto não embalado. Pedido explícito 2026-08-13 (revisado no
    /// mesmo dia): NÃO tira o pedido da fila — só troca a cor/texto do
    /// botão do card pra "Embalado" (ver OrderQueueCardViewModel.IsPacked).
    [JsonPropertyName("packed_at")]
    public DateTimeOffset? PackedAt { get; init; }

    /// Achado real 2026-08-15: a fila mostra TODO pedido do dia, qualquer
    /// status (ver DashboardAgentController::queue() no Laravel, sem
    /// whereNull/whereIn de status) — inclusive cancelado. Sem este campo
    /// (e sem tratamento nenhum na tela), um pedido cancelado DEPOIS de já
    /// aparecer aqui ficava visualmente idêntico a um ativo, botão "Em
    /// preparação" funcionando normal — risco real de embalar/despachar
    /// algo que a Shopee já cancelou. status_label já vem traduzido do
    /// servidor (mesmo texto usado no admin web), não precisa mapear aqui.
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("status_label")]
    public string? StatusLabel { get; init; }
}

public sealed class OrderQueueProductDto
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("quantity")]
    public required int Quantity { get; init; }

    [JsonPropertyName("sku")]
    public string? Sku { get; init; }
}
