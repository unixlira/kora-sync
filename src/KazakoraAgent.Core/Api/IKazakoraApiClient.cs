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

    Task<IReadOnlyList<ChannelOrderDto>> GetChannelOrdersAsync(string channel, CancellationToken ct = default);
}
