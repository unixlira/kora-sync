namespace KazakoraAgent.Core.Queue;

/// <summary>
/// Estado local (SQLite) de um PrintJob do servidor — é o que sobrevive a um
/// fechamento do app/reinício do PC, pra retomar a fila de onde parou.
/// </summary>
public sealed class QueuedJob
{
    public required long ServerJobId { get; init; }

    public required long OrderId { get; init; }

    /// Origem do pedido (loja/mercado_livre/shopee/tiktok_shop) — meramente
    /// informativo pra exibição na dashboard, não afeta o processamento.
    public string? Channel { get; set; }

    /// "express" ou "flex" — mesmo caso, informativo.
    public string? ShippingType { get; set; }

    public required QueuedJobStatus Status { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    /// Quando a próxima tentativa pode ocorrer — chegou (FIFO) não significa
    /// "pronto pra processar" se ainda estiver no meio de um backoff.
    public DateTimeOffset NextAttemptAt { get; set; }

    public required DateTimeOffset EnqueuedAt { get; init; }

    public DateTimeOffset? PrintedAt { get; set; }
}
