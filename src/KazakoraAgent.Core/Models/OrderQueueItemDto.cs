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
