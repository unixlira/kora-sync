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

    [JsonPropertyName("revenue_month")]
    public required decimal RevenueMonth { get; init; }

    /// Null quando não há venda no mesmo trecho do mês anterior pra
    /// comparar (ver DashboardAgentController::variationPct no Laravel) —
    /// não confundir com 0%.
    [JsonPropertyName("revenue_month_variation_pct")]
    public decimal? RevenueMonthVariationPct { get; init; }

    [JsonPropertyName("revenue_today_variation_pct")]
    public decimal? RevenueTodayVariationPct { get; init; }

    [JsonPropertyName("sales_today_variation_pct")]
    public decimal? SalesTodayVariationPct { get; init; }

    /// Contagem de pedidos com pagamento estornado no mês — proxy pra
    /// "devolução" (não existe integração real com devolução física/
    /// reclamação de marketplace ainda).
    [JsonPropertyName("returns_month")]
    public required int ReturnsMonth { get; init; }

    [JsonPropertyName("month_label")]
    public required string MonthLabel { get; init; }

    [JsonPropertyName("today_label")]
    public required string TodayLabel { get; init; }
}
