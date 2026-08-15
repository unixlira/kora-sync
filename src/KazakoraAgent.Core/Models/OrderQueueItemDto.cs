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

    /// "paid" | "shipped" | "completed" | "cancelled" | "pending" |
    /// "awaiting_payment" (Order::STATUS_* no Laravel). Pedido explícito
    /// 2026-08-15: a fila passou a trazer todo pedido de hoje, não só
    /// pago (DashboardAgentController::queue() não filtra mais por
    /// status) — BUG REAL achado em seguida: sem isto, o app deixava
    /// clicar "Em preparação" em pedido já enviado/cancelado, e o servidor
    /// rejeitava (409) tentando embalar — "o botão não funciona". Usado
    /// em OrderQueueCardViewModel.IsActionable pra só mostrar o botão em
    /// pedido pago.
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("status_label")]
    public required string StatusLabel { get; init; }
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
