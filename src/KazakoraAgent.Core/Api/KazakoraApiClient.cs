using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KazakoraAgent.Core.Models;

namespace KazakoraAgent.Core.Api;

public sealed class KazakoraApiClient : IKazakoraApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;

    /// <param name="http">
    /// BaseAddress já deve terminar em "/api/print-agent/" e o
    /// DefaultRequestHeaders.Authorization já deve estar setado como
    /// "Bearer {token}" — configuração fica em quem monta o HttpClient
    /// (App), não aqui, pra manter esta classe só sobre o protocolo HTTP.
    /// </param>
    public KazakoraApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<PrintJobDto>> GetQueuedJobsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetFromJsonAsync<JobsIndexResponse>("jobs", JsonOptions, ct)
            ?? throw new InvalidOperationException("Resposta vazia de GET jobs.");

        return response.Jobs;
    }

    public async Task ClaimJobAsync(long jobId, string agentId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"jobs/{jobId}/claim", new { agent_id = agentId }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<byte[]> DownloadLabelAsync(long jobId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"jobs/{jobId}/label", ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<byte[]?> DownloadArchiveAsync(long jobId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"jobs/{jobId}/archive", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task ReportCompleteAsync(long jobId, bool success, string? errorMessage, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"jobs/{jobId}/complete", new
        {
            status = success ? "printed" : "failed",
            error_message = errorMessage,
        }, ct);

        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<ChannelStatusDto>> GetChannelsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetFromJsonAsync<ChannelsResponse>("dashboard/channels", JsonOptions, ct)
            ?? throw new InvalidOperationException("Resposta vazia de GET dashboard/channels.");

        return response.Channels;
    }

    public async Task<DashboardMetricsDto> GetMetricsAsync(CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<DashboardMetricsDto>("dashboard/metrics", JsonOptions, ct)
            ?? throw new InvalidOperationException("Resposta vazia de GET dashboard/metrics.");
    }

    public async Task<IReadOnlyList<LabelDto>> GetLabelsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetFromJsonAsync<LabelsResponse>("dashboard/labels", JsonOptions, ct)
            ?? throw new InvalidOperationException("Resposta vazia de GET dashboard/labels.");

        return response.Labels;
    }

    public async Task<IReadOnlyList<OrderQueueItemDto>> GetOrderQueueAsync(CancellationToken ct = default)
    {
        var response = await _http.GetFromJsonAsync<QueueResponse>("dashboard/queue", JsonOptions, ct)
            ?? throw new InvalidOperationException("Resposta vazia de GET dashboard/queue.");

        return response.Queue;
    }

    public async Task PackOrderAsync(long orderId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"dashboard/queue/{orderId}/pack", content: null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<DailyTextDto?> GetDailyTextAsync(CancellationToken ct = default)
    {
        var response = await _http.GetFromJsonAsync<DailyTextResponse>("dashboard/daily-text", JsonOptions, ct)
            ?? throw new InvalidOperationException("Resposta vazia de GET dashboard/daily-text.");

        return response.DailyText;
    }

    public async Task<IReadOnlyList<ScheduledShipmentDto>> GetScheduledShipmentsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetFromJsonAsync<ScheduledShipmentsResponse>("dashboard/scheduled-shipments", JsonOptions, ct)
            ?? throw new InvalidOperationException("Resposta vazia de GET dashboard/scheduled-shipments.");

        return response.ScheduledShipments;
    }

    private sealed class LabelsResponse
    {
        [JsonPropertyName("labels")]
        public required List<LabelDto> Labels { get; init; }
    }

    private sealed class QueueResponse
    {
        [JsonPropertyName("queue")]
        public required List<OrderQueueItemDto> Queue { get; init; }
    }

    private sealed class DailyTextResponse
    {
        [JsonPropertyName("daily_text")]
        public DailyTextDto? DailyText { get; init; }
    }

    private sealed class ScheduledShipmentsResponse
    {
        [JsonPropertyName("scheduled_shipments")]
        public required List<ScheduledShipmentDto> ScheduledShipments { get; init; }
    }

    private sealed class JobsIndexResponse
    {
        [JsonPropertyName("jobs")]
        public required List<PrintJobDto> Jobs { get; init; }
    }

    private sealed class ChannelsResponse
    {
        [JsonPropertyName("channels")]
        public required List<ChannelStatusDto> Channels { get; init; }
    }
}
