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

    // Nulo pra etiqueta gerada manualmente (Kazakora Admin > Etiquetas
    // Manuais), sem pedido real associado — precisa ser nullable, senão a
    // desserialização de TODA a resposta de /jobs quebra silenciosamente
    // assim que um job desses aparece na fila (bug real em produção,
    // 2026-08-03: card "Na fila" ficava em 0 mesmo com job esperando).
    [JsonPropertyName("order_id")]
    public long? OrderId { get; init; }

    // Ambos nulos pra etiqueta manual (mesmo motivo do order_id acima) ou
    // pra pedido antigo anterior ao arquivamento local por rastreio
    // (2026-08-06) — nesse caso o agente simplesmente pula o passo de
    // arquivar em disco, sem quebrar o resto da fila.
    [JsonPropertyName("channel")]
    public string? Channel { get; init; }

    [JsonPropertyName("tracking_code")]
    public string? TrackingCode { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }
}
