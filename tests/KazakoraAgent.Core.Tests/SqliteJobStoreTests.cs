using KazakoraAgent.Core.Queue;
using Xunit;

namespace KazakoraAgent.Core.Tests;

public class SqliteJobStoreTests
{
    private static SqliteJobStore MakeStore() => new("Data Source=:memory:");

    [Fact]
    public async Task upsert_then_get_by_server_job_id_round_trips_all_fields()
    {
        using var store = MakeStore();
        var now = DateTimeOffset.UtcNow;

        var job = new QueuedJob
        {
            ServerJobId = 42,
            OrderId = 7,
            Channel = "mercado_livre",
            ShippingType = "flex",
            Status = QueuedJobStatus.Queued,
            AttemptCount = 0,
            NextAttemptAt = now,
            EnqueuedAt = now,
        };

        await store.UpsertAsync(job);
        var fetched = await store.GetByServerJobIdAsync(42);

        Assert.NotNull(fetched);
        Assert.Equal(7, fetched!.OrderId);
        Assert.Equal("mercado_livre", fetched.Channel);
        Assert.Equal("flex", fetched.ShippingType);
        Assert.Equal(QueuedJobStatus.Queued, fetched.Status);
    }

    [Fact]
    public async Task upsert_on_existing_job_updates_in_place_not_duplicates()
    {
        using var store = MakeStore();
        var now = DateTimeOffset.UtcNow;

        var job = new QueuedJob { ServerJobId = 1, OrderId = 1, Status = QueuedJobStatus.Queued, NextAttemptAt = now, EnqueuedAt = now };
        await store.UpsertAsync(job);

        job.Status = QueuedJobStatus.WaitingRetry;
        job.AttemptCount = 2;
        await store.UpsertAsync(job);

        var all = await store.GetAllAsync();

        Assert.Single(all);
        Assert.Equal(QueuedJobStatus.WaitingRetry, all[0].Status);
        Assert.Equal(2, all[0].AttemptCount);
    }

    [Fact]
    public async Task get_next_due_job_returns_fifo_order_by_enqueued_at()
    {
        using var store = MakeStore();
        var now = DateTimeOffset.UtcNow;

        await store.UpsertAsync(new QueuedJob { ServerJobId = 2, OrderId = 2, Status = QueuedJobStatus.Queued, NextAttemptAt = now, EnqueuedAt = now.AddSeconds(2) });
        await store.UpsertAsync(new QueuedJob { ServerJobId = 1, OrderId = 1, Status = QueuedJobStatus.Queued, NextAttemptAt = now, EnqueuedAt = now.AddSeconds(1) });

        var next = await store.GetNextDueJobAsync(now.AddSeconds(10));

        Assert.NotNull(next);
        Assert.Equal(1, next!.ServerJobId);
    }

    [Fact]
    public async Task get_next_due_job_ignores_jobs_still_waiting_on_backoff()
    {
        using var store = MakeStore();
        var now = DateTimeOffset.UtcNow;

        await store.UpsertAsync(new QueuedJob
        {
            ServerJobId = 1,
            OrderId = 1,
            Status = QueuedJobStatus.WaitingRetry,
            NextAttemptAt = now.AddSeconds(30),
            EnqueuedAt = now,
        });

        var dueNow = await store.GetNextDueJobAsync(now);
        Assert.Null(dueNow);

        var dueLater = await store.GetNextDueJobAsync(now.AddSeconds(31));
        Assert.NotNull(dueLater);
    }

    [Fact]
    public async Task get_next_due_job_skips_jobs_that_are_already_printed_or_failed_permanently()
    {
        using var store = MakeStore();
        var now = DateTimeOffset.UtcNow;

        await store.UpsertAsync(new QueuedJob { ServerJobId = 1, OrderId = 1, Status = QueuedJobStatus.Printed, NextAttemptAt = now, EnqueuedAt = now });
        await store.UpsertAsync(new QueuedJob { ServerJobId = 2, OrderId = 2, Status = QueuedJobStatus.FailedPermanently, NextAttemptAt = now, EnqueuedAt = now });

        var next = await store.GetNextDueJobAsync(now.AddMinutes(1));

        Assert.Null(next);
    }
}
