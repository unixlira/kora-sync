using System.Text.Json.Serialization;

namespace KazakoraAgent.Core.Models;

/// <summary>
/// Espelha um item de GET /api/print-agent/dashboard/channels/{channel}/orders
/// (DashboardAgentController::channelOrders no Laravel).
/// </summary>
public sealed class ChannelOrderDto
{
    [JsonPropertyName("id")]
    public required long Id { get; init; }

    [JsonPropertyName("external_order_id")]
    public string? ExternalOrderId { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("customer_name")]
    public required string CustomerName { get; init; }

    [JsonPropertyName("products")]
    public required List<ChannelOrderProductDto> Products { get; init; }

    [JsonPropertyName("gross_amount")]
    public required decimal GrossAmount { get; init; }

    [JsonPropertyName("fee_amount")]
    public decimal? FeeAmount { get; init; }

    [JsonPropertyName("net_amount")]
    public decimal? NetAmount { get; init; }

    [JsonPropertyName("shipping_method")]
    public string? ShippingMethod { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class ChannelOrderProductDto
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("quantity")]
    public required int Quantity { get; init; }
}
