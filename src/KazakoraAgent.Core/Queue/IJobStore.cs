namespace KazakoraAgent.Core.Queue;

public interface IJobStore
{
    Task UpsertAsync(QueuedJob job, CancellationToken ct = default);

    Task<QueuedJob?> GetByServerJobIdAsync(long serverJobId, CancellationToken ct = default);

    Task<IReadOnlyList<QueuedJob>> GetAllAsync(CancellationToken ct = default);

    /// FIFO: o próximo job pronto pra processar agora (Queued ou WaitingRetry
    /// com NextAttemptAt já vencido), ordenado por EnqueuedAt — chegou
    /// primeiro, processa primeiro, sem depender do canal de origem.
    Task<QueuedJob?> GetNextDueJobAsync(DateTimeOffset now, CancellationToken ct = default);

    /// Apaga jobs já finalizados (impressos ou falha permanente) mais
    /// antigos que <paramref name="olderThan"/> — sem isso o banco local
    /// cresce pra sempre, mesmo guardando só metadado (nunca dado de
    /// cliente/produto). Retorna quantas linhas foram removidas.
    Task<int> DeleteOldTerminalJobsAsync(DateTimeOffset olderThan, CancellationToken ct = default);
}
