using Microsoft.Data.Sqlite;

namespace KazakoraAgent.Core.Queue;

/// <summary>
/// Guarda o estado da fila localmente — é o que permite retomar de onde
/// parou se o app fechar ou o PC reiniciar (requisito de resiliência).
/// Mantém UMA conexão aberta pela vida do store (inclusive pra
/// "Data Source=:memory:" nos testes, que reseta o banco a cada nova
/// conexão se não fizer isso).
/// </summary>
public sealed class SqliteJobStore : IJobStore, IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteJobStore(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS jobs (
                server_job_id INTEGER PRIMARY KEY,
                order_id INTEGER NOT NULL,
                channel TEXT NULL,
                shipping_type TEXT NULL,
                status TEXT NOT NULL,
                attempt_count INTEGER NOT NULL DEFAULT 0,
                last_error TEXT NULL,
                next_attempt_at TEXT NOT NULL,
                enqueued_at TEXT NOT NULL,
                printed_at TEXT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public Task UpsertAsync(QueuedJob job, CancellationToken ct = default)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO jobs (server_job_id, order_id, channel, shipping_type, status, attempt_count, last_error, next_attempt_at, enqueued_at, printed_at)
            VALUES ($serverJobId, $orderId, $channel, $shippingType, $status, $attemptCount, $lastError, $nextAttemptAt, $enqueuedAt, $printedAt)
            ON CONFLICT(server_job_id) DO UPDATE SET
                channel = excluded.channel,
                shipping_type = excluded.shipping_type,
                status = excluded.status,
                attempt_count = excluded.attempt_count,
                last_error = excluded.last_error,
                next_attempt_at = excluded.next_attempt_at,
                printed_at = excluded.printed_at;
            """;

        command.Parameters.AddWithValue("$serverJobId", job.ServerJobId);
        command.Parameters.AddWithValue("$orderId", job.OrderId);
        command.Parameters.AddWithValue("$channel", (object?) job.Channel ?? DBNull.Value);
        command.Parameters.AddWithValue("$shippingType", (object?) job.ShippingType ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", job.Status.ToString());
        command.Parameters.AddWithValue("$attemptCount", job.AttemptCount);
        command.Parameters.AddWithValue("$lastError", (object?) job.LastError ?? DBNull.Value);
        command.Parameters.AddWithValue("$nextAttemptAt", job.NextAttemptAt.ToString("o"));
        command.Parameters.AddWithValue("$enqueuedAt", job.EnqueuedAt.ToString("o"));
        command.Parameters.AddWithValue("$printedAt", (object?) job.PrintedAt?.ToString("o") ?? DBNull.Value);

        command.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task<QueuedJob?> GetByServerJobIdAsync(long serverJobId, CancellationToken ct = default)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT * FROM jobs WHERE server_job_id = $serverJobId;";
        command.Parameters.AddWithValue("$serverJobId", serverJobId);

        using var reader = command.ExecuteReader();

        return Task.FromResult(reader.Read() ? Map(reader) : null);
    }

    public Task<IReadOnlyList<QueuedJob>> GetAllAsync(CancellationToken ct = default)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT * FROM jobs ORDER BY enqueued_at ASC;";

        using var reader = command.ExecuteReader();
        var results = new List<QueuedJob>();

        while (reader.Read())
        {
            results.Add(Map(reader));
        }

        return Task.FromResult<IReadOnlyList<QueuedJob>>(results);
    }

    public Task<QueuedJob?> GetNextDueJobAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM jobs
            WHERE status IN ($queued, $waitingRetry) AND next_attempt_at <= $now
            ORDER BY enqueued_at ASC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$queued", QueuedJobStatus.Queued.ToString());
        command.Parameters.AddWithValue("$waitingRetry", QueuedJobStatus.WaitingRetry.ToString());
        command.Parameters.AddWithValue("$now", now.ToString("o"));

        using var reader = command.ExecuteReader();

        return Task.FromResult(reader.Read() ? Map(reader) : null);
    }

    private static QueuedJob Map(SqliteDataReader reader)
    {
        return new QueuedJob
        {
            ServerJobId = reader.GetInt64(reader.GetOrdinal("server_job_id")),
            OrderId = reader.GetInt64(reader.GetOrdinal("order_id")),
            Channel = reader.IsDBNull(reader.GetOrdinal("channel")) ? null : reader.GetString(reader.GetOrdinal("channel")),
            ShippingType = reader.IsDBNull(reader.GetOrdinal("shipping_type")) ? null : reader.GetString(reader.GetOrdinal("shipping_type")),
            Status = Enum.Parse<QueuedJobStatus>(reader.GetString(reader.GetOrdinal("status"))),
            AttemptCount = reader.GetInt32(reader.GetOrdinal("attempt_count")),
            LastError = reader.IsDBNull(reader.GetOrdinal("last_error")) ? null : reader.GetString(reader.GetOrdinal("last_error")),
            NextAttemptAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("next_attempt_at"))),
            EnqueuedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("enqueued_at"))),
            PrintedAt = reader.IsDBNull(reader.GetOrdinal("printed_at")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("printed_at"))),
        };
    }

    public void Dispose() => _connection.Dispose();
}
