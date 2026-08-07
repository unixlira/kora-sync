using KazakoraAgent.Core.Api;
using KazakoraAgent.Core.Models;
using KazakoraAgent.Core.Printing;
using KazakoraAgent.Core.Queue;
using Moq;
using Xunit;

namespace KazakoraAgent.Core.Tests;

public class QueueEngineTests
{
    private static QueuedJob MakeJob(long serverJobId, DateTimeOffset enqueuedAt, QueuedJobStatus status = QueuedJobStatus.Queued, int attemptCount = 0) => new()
    {
        ServerJobId = serverJobId,
        OrderId = serverJobId,
        Status = status,
        AttemptCount = attemptCount,
        NextAttemptAt = enqueuedAt,
        EnqueuedAt = enqueuedAt,
    };

    [Fact]
    public async Task sync_from_server_claims_and_persists_new_jobs_and_skips_already_known_ones()
    {
        var now = DateTimeOffset.UtcNow;
        var time = new ManualTimeProvider(now);
        using var store = new SqliteJobStore("Data Source=:memory:");
        await store.UpsertAsync(MakeJob(1, now));

        var api = new Mock<IKazakoraApiClient>();
        api.Setup(a => a.GetQueuedJobsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new PrintJobDto { Id = 1, OrderId = 1, CreatedAt = now },
            new PrintJobDto { Id = 2, OrderId = 2, CreatedAt = now },
        ]);

        var engine = new QueueEngine(api.Object, store, Mock.Of<IPrinter>(), new RetryPolicy(), "agent-1", _ => "printer", timeProvider: time);

        await engine.SyncFromServerAsync();

        api.Verify(a => a.ClaimJobAsync(1, "agent-1", It.IsAny<CancellationToken>()), Times.Never);
        api.Verify(a => a.ClaimJobAsync(2, "agent-1", It.IsAny<CancellationToken>()), Times.Once);

        var all = await store.GetAllAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task process_next_due_job_on_success_prints_reports_complete_and_marks_printed()
    {
        var now = DateTimeOffset.UtcNow;
        var time = new ManualTimeProvider(now);
        using var store = new SqliteJobStore("Data Source=:memory:");
        await store.UpsertAsync(MakeJob(1, now));

        var label = new byte[] { 1, 2, 3 };
        var api = new Mock<IKazakoraApiClient>();
        api.Setup(a => a.DownloadLabelAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(label);

        var printer = new Mock<IPrinter>();
        printer.Setup(p => p.PrintAsync(label, "KazaKora-Printer", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var engine = new QueueEngine(api.Object, store, printer.Object, new RetryPolicy(), "agent-1", _ => "KazaKora-Printer", timeProvider: time);

        QueuedJob? printedEvent = null;
        engine.JobPrinted += job => printedEvent = job;

        var processed = await engine.ProcessNextDueJobAsync();

        Assert.True(processed);
        Assert.NotNull(printedEvent);
        api.Verify(a => a.ReportCompleteAsync(1, true, null, It.IsAny<CancellationToken>()), Times.Once);

        var stored = await store.GetByServerJobIdAsync(1);
        Assert.Equal(QueuedJobStatus.Printed, stored!.Status);
        Assert.Equal(1, stored.AttemptCount);
    }

    [Fact]
    public async Task process_next_due_job_on_success_archives_the_raw_label_locally_when_archiving_is_configured()
    {
        var now = DateTimeOffset.UtcNow;
        var time = new ManualTimeProvider(now);
        using var store = new SqliteJobStore("Data Source=:memory:");
        var job = MakeJob(1, now);
        job.Channel = "shopee";
        job.TrackingCode = "BR123456789";
        await store.UpsertAsync(job);

        var label = new byte[] { 1, 2, 3 };
        var rawZip = new byte[] { 0x50, 0x4B, 0x03, 0x04, 9, 9 };
        var api = new Mock<IKazakoraApiClient>();
        api.Setup(a => a.DownloadLabelAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(label);
        api.Setup(a => a.DownloadArchiveAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(rawZip);

        var archiveRoot = Path.Combine(Path.GetTempPath(), $"korasync-archive-test-{Guid.NewGuid():N}");

        try
        {
            var engine = new QueueEngine(
                api.Object, store, Mock.Of<IPrinter>(), new RetryPolicy(), "agent-1",
                _ => "printer", _ => archiveRoot, timeProvider: time);

            string? archivedPath = null;
            engine.JobArchived += (_, path) => archivedPath = path;

            await engine.ProcessNextDueJobAsync();

            Assert.NotNull(archivedPath);
            Assert.True(File.Exists(archivedPath));
            Assert.EndsWith("BR123456789.zip", archivedPath);
            Assert.Contains(Path.Combine("Shopee"), archivedPath);
        }
        finally
        {
            if (Directory.Exists(archiveRoot))
            {
                Directory.Delete(archiveRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task process_next_due_job_on_failure_schedules_retry_using_the_policy_and_does_not_report_to_server_yet()
    {
        var now = DateTimeOffset.UtcNow;
        var time = new ManualTimeProvider(now);
        using var store = new SqliteJobStore("Data Source=:memory:");
        await store.UpsertAsync(MakeJob(1, now));

        var api = new Mock<IKazakoraApiClient>();
        api.Setup(a => a.DownloadLabelAsync(1, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("timeout"));

        var engine = new QueueEngine(api.Object, store, Mock.Of<IPrinter>(), new RetryPolicy(), "agent-1", _ => "printer", timeProvider: time);

        QueuedJob? retryingEvent = null;
        engine.JobRetrying += job => retryingEvent = job;

        await engine.ProcessNextDueJobAsync();

        Assert.NotNull(retryingEvent);
        api.Verify(a => a.ReportCompleteAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

        var stored = await store.GetByServerJobIdAsync(1);
        Assert.Equal(QueuedJobStatus.WaitingRetry, stored!.Status);
        Assert.Equal(1, stored.AttemptCount);
        Assert.Equal(now + TimeSpan.FromSeconds(10), stored.NextAttemptAt);
        Assert.Equal("timeout", stored.LastError);
    }

    [Fact]
    public async Task process_next_due_job_marks_failed_permanently_after_5_attempts_and_reports_failure_to_server()
    {
        var now = DateTimeOffset.UtcNow;
        var time = new ManualTimeProvider(now);
        using var store = new SqliteJobStore("Data Source=:memory:");
        await store.UpsertAsync(MakeJob(1, now, QueuedJobStatus.WaitingRetry, attemptCount: 4));

        var api = new Mock<IKazakoraApiClient>();
        api.Setup(a => a.DownloadLabelAsync(1, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("erro definitivo"));

        var engine = new QueueEngine(api.Object, store, Mock.Of<IPrinter>(), new RetryPolicy(), "agent-1", _ => "printer", timeProvider: time);

        QueuedJob? failedEvent = null;
        engine.JobFailedPermanently += job => failedEvent = job;

        await engine.ProcessNextDueJobAsync();

        Assert.NotNull(failedEvent);
        api.Verify(a => a.ReportCompleteAsync(1, false, "erro definitivo", It.IsAny<CancellationToken>()), Times.Once);

        var stored = await store.GetByServerJobIdAsync(1);
        Assert.Equal(QueuedJobStatus.FailedPermanently, stored!.Status);
        Assert.Equal(5, stored.AttemptCount);
    }

    [Fact]
    public async Task a_job_waiting_on_backoff_does_not_block_the_next_queued_job_fifo_non_blocking()
    {
        var now = DateTimeOffset.UtcNow;
        var time = new ManualTimeProvider(now);
        using var store = new SqliteJobStore("Data Source=:memory:");

        // Job 1 chegou primeiro mas já está em retry (backoff ainda não venceu).
        var job1WaitingRetry = MakeJob(1, now, QueuedJobStatus.WaitingRetry, attemptCount: 1);
        job1WaitingRetry.NextAttemptAt = now.AddSeconds(10);
        await store.UpsertAsync(job1WaitingRetry);
        // Job 2 chegou depois, mas está pronto pra processar agora.
        await store.UpsertAsync(MakeJob(2, now.AddSeconds(1)));

        var api = new Mock<IKazakoraApiClient>();
        api.Setup(a => a.DownloadLabelAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([9]);

        var engine = new QueueEngine(api.Object, store, Mock.Of<IPrinter>(), new RetryPolicy(), "agent-1", _ => "printer", timeProvider: time);

        // Avança só o suficiente pra job2 (devido em now+1s) ficar pronto,
        // sem chegar nos now+10s que job1 precisa pra sair do backoff.
        time.Advance(TimeSpan.FromSeconds(2));

        var processed = await engine.ProcessNextDueJobAsync();

        Assert.True(processed);
        var job2 = await store.GetByServerJobIdAsync(2);
        Assert.Equal(QueuedJobStatus.Printed, job2!.Status);

        var job1 = await store.GetByServerJobIdAsync(1);
        Assert.Equal(QueuedJobStatus.WaitingRetry, job1!.Status);
    }
}
