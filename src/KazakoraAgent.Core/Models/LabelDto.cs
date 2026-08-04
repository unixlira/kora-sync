using System.Text.Json.Serialization;

namespace KazakoraAgent.Core.Models;

/// <summary>
/// Espelha um item de GET /api/print-agent/dashboard/labels
/// (DashboardAgentController::labels no Laravel) — histórico recente de
/// PrintJob de qualquer status, pra exibição na lista de etiquetas.
/// Separado de propósito de PrintJobDto (usado pelo loop real de
/// impressão, só QUEUED, campos mínimos).
/// </summary>
public sealed class LabelDto
{
    [JsonPropertyName("id")]
    public required long Id { get; init; }

    [JsonPropertyName("channel")]
    public string? Channel { get; init; }

    [JsonPropertyName("order_id")]
    public long? OrderId { get; init; }

    [JsonPropertyName("external_order_id")]
    public string? ExternalOrderId { get; init; }

    [JsonPropertyName("products")]
    public required List<LabelProductDto> Products { get; init; }

    /// "queued" | "claimed" | "printed" | "failed" — mesmo vocabulário de
    /// PrintJob::STATUS_* no Laravel.
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("printed_at")]
    public DateTimeOffset? PrintedAt { get; init; }
}

public sealed class LabelProductDto
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("quantity")]
    public required int Quantity { get; init; }

    [JsonPropertyName("sku")]
    public string? Sku { get; init; }
}
