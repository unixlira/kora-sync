using KazakoraAgent.Core.Api;
using KazakoraAgent.Core.Printing;

namespace KazakoraAgent.Core.Queue;

/// <summary>
/// Orquestra a fila FIFO não-bloqueante com retentativa exponencial.
/// Um job em WaitingRetry não bloqueia os demais — GetNextDueJobAsync só
/// retorna jobs cujo NextAttemptAt já venceu, então o próximo da fila
/// (Queued) é pego normalmente enquanto outro aguarda seu backoff.
/// </summary>
public sealed class QueueEngine
{
    private readonly IKazakoraApiClient _api;
    private readonly IJobStore _store;
    private readonly IPrinter _printer;
    private readonly RetryPolicy _retryPolicy;
    private readonly string _agentId;
    private readonly Func<QueuedJob, string> _printerNameSelector;
    private readonly TimeProvider _timeProvider;

    public event Action<QueuedJob>? JobPrinted;

    public event Action<QueuedJob>? JobRetrying;

    /// Falha permanente (esgotou tentativas) — quem assina isso é quem
    /// dispara o alerta visual na dashboard + notificação do Windows.
    public event Action<QueuedJob>? JobFailedPermanently;

    public QueueEngine(
        IKazakoraApiClient api,
        IJobStore store,
        IPrinter printer,
        RetryPolicy retryPolicy,
        string agentId,
        Func<QueuedJob, string> printerNameSelector,
        TimeProvider? timeProvider = null)
    {
        _api = api;
        _store = store;
        _printer = printer;
        _retryPolicy = retryPolicy;
        _agentId = agentId;
        _printerNameSelector = printerNameSelector;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// Puxa jobs novos (status "queued" no servidor) que ainda não conhecemos
    /// localmente, reivindica cada um (claim) e entra na fila local.
    public async Task SyncFromServerAsync(CancellationToken ct = default)
    {
        var serverJobs = await _api.GetQueuedJobsAsync(ct);

        foreach (var serverJob in serverJobs)
        {
            var existing = await _store.GetByServerJobIdAsync(serverJob.Id, ct);

            if (existing is not null)
            {
                continue;
            }

            await _api.ClaimJobAsync(serverJob.Id, _agentId, ct);

            var now = _timeProvider.GetUtcNow();

            await _store.UpsertAsync(new QueuedJob
            {
                ServerJobId = serverJob.Id,
                OrderId = serverJob.OrderId,
                Status = QueuedJobStatus.Queued,
                AttemptCount = 0,
                NextAttemptAt = now,
                EnqueuedAt = now,
            }, ct);
        }
    }

    /// Ação manual do botão "Imprimir etiqueta" na lista de etiquetas —
    /// pula o backoff de um job em espera/retentativa/falha permanente,
    /// marcando NextAttemptAt como agora (falha permanente também ganha um
    /// novo orçamento de tentativas, já que "tentar de novo" é exatamente
    /// o que o clique pediu). Não processa na hora — só torna o job
    /// elegível pro próximo tick de ProcessNextDueJobAsync, que já roda a
    /// cada poucos segundos. Retorna false se o job não existe localmente
    /// (ex: de outra máquina) ou já está em Processing/Printed (nada a
    /// fazer/reprocessar).
    public async Task<bool> RequestImmediateRetryAsync(long serverJobId, CancellationToken ct = default)
    {
        var job = await _store.GetByServerJobIdAsync(serverJobId, ct);

        if (job is null || job.Status is QueuedJobStatus.Processing or QueuedJobStatus.Printed)
        {
            return false;
        }

        if (job.Status == QueuedJobStatus.FailedPermanently)
        {
            job.Status = QueuedJobStatus.WaitingRetry;
            job.AttemptCount = 0;
        }

        job.NextAttemptAt = _timeProvider.GetUtcNow();
        await _store.UpsertAsync(job, ct);

        return true;
    }

    /// Processa um único job pronto (o mais antigo dentre os devidos), se
    /// houver. Retorna false se a fila local não tem nada pronto agora.
    public async Task<bool> ProcessNextDueJobAsync(CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        var job = await _store.GetNextDueJobAsync(now, ct);

        if (job is null)
        {
            return false;
        }

        job.Status = QueuedJobStatus.Processing;
        job.AttemptCount++;
        await _store.UpsertAsync(job, ct);

        try
        {
            var label = await _api.DownloadLabelAsync(job.ServerJobId, ct);
            var printerName = _printerNameSelector(job);
            await _printer.PrintAsync(label, printerName, ct);

            job.Status = QueuedJobStatus.Printed;
            job.PrintedAt = now;
            job.LastError = null;
            await _store.UpsertAsync(job, ct);

            await _api.ReportCompleteAsync(job.ServerJobId, success: true, errorMessage: null, ct);

            JobPrinted?.Invoke(job);
        }
        catch (Exception ex)
        {
            job.LastError = ex.Message;

            if (_retryPolicy.ShouldRetry(job.AttemptCount))
            {
                job.Status = QueuedJobStatus.WaitingRetry;
                job.NextAttemptAt = now + _retryPolicy.DelayForNextAttempt(job.AttemptCount);
                await _store.UpsertAsync(job, ct);

                JobRetrying?.Invoke(job);
            }
            else
            {
                job.Status = QueuedJobStatus.FailedPermanently;
                await _store.UpsertAsync(job, ct);

                await _api.ReportCompleteAsync(job.ServerJobId, success: false, errorMessage: job.LastError, ct);

                JobFailedPermanently?.Invoke(job);
            }
        }

        return true;
    }
}
