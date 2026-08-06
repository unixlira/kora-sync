using KazakoraAgent.Core.Models;

namespace KazakoraAgent.Core.Api;

/// <summary>
/// Espelha o protocolo já existente em PrintAgentController (Laravel) +
/// os novos endpoints de dashboard. Abstraído atrás de interface pra o
/// motor de fila (QueueEngine) ser testável sem rede.
/// </summary>
public interface IKazakoraApiClient
{
    Task<IReadOnlyList<PrintJobDto>> GetQueuedJobsAsync(CancellationToken ct = default);

    Task ClaimJobAsync(long jobId, string agentId, CancellationToken ct = default);

    Task<byte[]> DownloadLabelAsync(long jobId, CancellationToken ct = default);

    Task ReportCompleteAsync(long jobId, bool success, string? errorMessage, CancellationToken ct = default);

    Task<IReadOnlyList<ChannelStatusDto>> GetChannelsAsync(CancellationToken ct = default);

    Task<DashboardMetricsDto> GetMetricsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<LabelDto>> GetLabelsAsync(CancellationToken ct = default);

    /// Fila de expedição de HOJE (pedido explícito 2026-08-06) — pedido
    /// pago, ainda não embalado/enviado, ordem decrescente (mais recente
    /// primeiro). Alimenta os 2 cards em destaque + a lista com scroll do
    /// resto do dia.
    Task<IReadOnlyList<OrderQueueItemDto>> GetOrderQueueAsync(CancellationToken ct = default);

    /// Null quando o servidor ainda não tem nenhum texto diário salvo.
    Task<DailyTextDto?> GetDailyTextAsync(CancellationToken ct = default);
}
