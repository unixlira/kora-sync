using System.Text.Json.Serialization;

namespace KazakoraAgent.Core.Models;

/// <summary>
/// Espelha um item de GET /api/print-agent/dashboard/queue
/// (DashboardAgentController::queue no Laravel) — TODO pedido de hoje,
/// qualquer status (pedido explícito 2026-08-15, era só "pago" antes).
/// Filtrado só por data (hoje), exclusivo desse fluxo nativo (pedido
/// explícito 2026-08-06).
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
    /// "awaiting_payment" — mesmo vocabulário de Order::STATUS_* no
    /// Laravel. Pedido explícito 2026-08-15: a fila passou a trazer
    /// qualquer status, não só pago — o app usa isto (não Status ==
    /// "paid") pra decidir se mostra o botão "Em preparação" (só faz
    /// sentido embalar um pedido pago, ver OrderQueueCardViewModel.
    /// IsActionable) ou só o rótulo do status.
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
