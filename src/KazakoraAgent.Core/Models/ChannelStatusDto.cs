using System.Text.Json.Serialization;

namespace KazakoraAgent.Core.Models;

/// <summary>
/// Espelha um item de GET /api/print-agent/dashboard/channels
/// (DashboardAgentController::channels no Laravel).
/// </summary>
public sealed class ChannelStatusDto
{
    [JsonPropertyName("channel")]
    public required string Channel { get; init; }

    [JsonPropertyName("connected")]
    public required bool Connected { get; init; }

    [JsonPropertyName("last_order")]
    public LastOrderDto? LastOrder { get; init; }

    [JsonPropertyName("last_label_printed_at")]
    public DateTimeOffset? LastLabelPrintedAt { get; init; }

    [JsonPropertyName("labels_printed_today")]
    public required int LabelsPrintedToday { get; init; }

    [JsonPropertyName("revenue_month")]
    public required decimal RevenueMonth { get; init; }

    [JsonPropertyName("revenue_today")]
    public required decimal RevenueToday { get; init; }

    [JsonPropertyName("sales_month")]
    public required int SalesMonth { get; init; }

    [JsonPropertyName("orders_today")]
    public required int OrdersToday { get; init; }

    /// Mesma definição de "devolução" do DashboardMetricsDto: pedido com
    /// pagamento estornado no mês, por canal.
    [JsonPropertyName("returns_month")]
    public required int ReturnsMonth { get; init; }
}

public sealed class LastOrderDto
{
    [JsonPropertyName("id")]
    public required long Id { get; init; }

    [JsonPropertyName("external_order_id")]
    public string? ExternalOrderId { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }
}
