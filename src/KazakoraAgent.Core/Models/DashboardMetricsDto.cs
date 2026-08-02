using System.Text.Json.Serialization;

namespace KazakoraAgent.Core.Models;

/// <summary>
/// Espelha GET /api/print-agent/dashboard/metrics
/// (DashboardAgentController::metrics no Laravel).
/// </summary>
public sealed class DashboardMetricsDto
{
    [JsonPropertyName("revenue_today")]
    public required decimal RevenueToday { get; init; }

    [JsonPropertyName("sales_today")]
    public required int SalesToday { get; init; }

    [JsonPropertyName("cancelled_today")]
    public required int CancelledToday { get; init; }

    [JsonPropertyName("refunded_today")]
    public required int RefundedToday { get; init; }

    [JsonPropertyName("cart_items_count")]
    public required int CartItemsCount { get; init; }

    /// Aproximação (bruto - taxas de marketplace capturadas), não lucro
    /// real — o Kazakora ainda não tem custo de produto cadastrado. Ver
    /// comentário em DashboardAgentController::metrics() no Laravel.
    [JsonPropertyName("net_profit_today")]
    public required decimal NetProfitToday { get; init; }
}
