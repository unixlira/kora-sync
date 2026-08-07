using KazakoraAgent.Core.Api;
using KazakoraAgent.Core.Archiving;
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
    private readonly Func<QueuedJob, string?> _archiveRootSelector;
    private readonly TimeProvider _timeProvider;

    public event Action<QueuedJob>? JobPrinted;

    /// Arquivamento local (zip/pdf bruto salvo em disco) — separado de
    /// JobPrinted porque é best-effort e não deve gerar notificação nem
    /// afetar o resultado do job em si, só é útil pro log em arquivo.
    public event Action<QueuedJob, string>? JobArchived;

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
        Func<QueuedJob, string?>? archiveRootSelector = null,
        TimeProvider? timeProvider = null)
    {
        _api = api;
        _store = store;
        _printer = printer;
        _retryPolicy = retryPolicy;
        _agentId = agentId;
        _printerNameSelector = printerNameSelector;
        // Sem seletor informado (ex: testes), arquivamento fica desligado —
        // nunca null-refs, só não arquiva nada.
        _archiveRootSelector = archiveRootSelector ?? (_ => null);
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
                Channel = serverJob.Channel,
                TrackingCode = serverJob.TrackingCode,
                Status = QueuedJobStatus.Queued,
                AttemptCount = 0,
                NextAttemptAt = now,
                EnqueuedAt = now,
            }, ct);
        }
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

            // Arquivamento local ANTES de reportar conclusão — o servidor só
            // libera o download do arquivo bruto enquanto o job ainda está
            // "claimed" (mesma trava do /label). Best-effort de propósito:
            // uma falha aqui (pasta sem permissão, disco cheio, servidor
            // sem o arquivo bruto pra pedido antigo) não pode derrubar um
            // job que JÁ imprimiu de verdade.
            await TryArchiveAsync(job, now, ct);

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

    private async Task TryArchiveAsync(QueuedJob job, DateTimeOffset when, CancellationToken ct)
    {
        var archiveRoot = _archiveRootSelector(job);

        if (string.IsNullOrWhiteSpace(archiveRoot))
        {
            return;
        }

        try
        {
            var contents = await _api.DownloadArchiveAsync(job.ServerJobId, ct);

            if (contents is null)
            {
                // 404 — etiqueta manual ou pedido anterior à feature, sem
                // arquivo bruto pra arquivar. Não é erro.
                return;
            }

            var savedPath = SalesArchiveService.Save(archiveRoot, job.Channel, job.TrackingCode, job.OrderId, when, contents);

            JobArchived?.Invoke(job, savedPath);
        }
        catch (Exception ex)
        {
            KazakoraAgent.Core.AppLog.Error($"Arquivamento local do job {job.ServerJobId} (pedido #{job.OrderId}) falhou — impressão OK, só a cópia em disco que não foi salva: {ex.Message}");
        }
    }
}
