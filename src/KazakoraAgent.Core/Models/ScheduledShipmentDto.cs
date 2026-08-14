using System.Text.Json.Serialization;

namespace KazakoraAgent.Core.Models;

/// <summary>
/// Espelha um item de GET /api/print-agent/dashboard/scheduled-shipments
/// (DashboardAgentController::scheduledShipments no Laravel) — venda que o
/// PRÓPRIO CANAL (Coleta/Places do Mercado Livre, até agora) decidiu
/// agendar a liberação da etiqueta pra uma data futura, achado real
/// 2026-08-14 (pedido #278, agendado pro dia 17 — ninguém do time sabia
/// por que a etiqueta não saía e parecia um pedido travado de verdade).
/// Sem essa lista, esse tipo de venda ficava indistinguível de um pedido
/// travado só olhando a fila de preparação.
/// </summary>
public sealed class ScheduledShipmentDto
{
    [JsonPropertyName("order_id")]
    public required long OrderId { get; init; }

    [JsonPropertyName("external_order_id")]
    public string? ExternalOrderId { get; init; }

    [JsonPropertyName("channel")]
    public string? Channel { get; init; }

    [JsonPropertyName("customer_name")]
    public string? CustomerName { get; init; }

    [JsonPropertyName("shipping_method")]
    public string? ShippingMethod { get; init; }

    [JsonPropertyName("scheduled_for")]
    public required DateTimeOffset ScheduledFor { get; init; }

    /// True quando scheduled_for já passou e o canal ainda não liberou —
    /// diferente de "vai liberar em breve" (aviso tranquilo), isso aqui é
    /// hora de prestar atenção de verdade.
    [JsonPropertyName("is_overdue")]
    public required bool IsOverdue { get; init; }

    [JsonPropertyName("products")]
    public required List<ScheduledShipmentProductDto> Products { get; init; }
}

public sealed class ScheduledShipmentProductDto
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("quantity")]
    public required int Quantity { get; init; }
}
