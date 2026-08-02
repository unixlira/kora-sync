using System.Text.Json.Serialization;

namespace KazakoraAgent.Core.Models;

/// <summary>
/// Espelha o shape retornado por GET /api/print-agent/jobs no Laravel
/// (PrintAgentController::index) — apenas jobs com status "queued".
/// </summary>
public sealed class PrintJobDto
{
    [JsonPropertyName("id")]
    public required long Id { get; init; }

    [JsonPropertyName("order_id")]
    public required long OrderId { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }
}
