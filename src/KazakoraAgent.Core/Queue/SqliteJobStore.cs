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
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS jobs (
                    server_job_id INTEGER PRIMARY KEY,
                    order_id INTEGER NULL,
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

        MigrateOrderIdToNullableIfNeeded();
        AddTrackingCodeColumnIfNeeded();
        AddSaleIdColumnIfNeeded();
    }

    /// <summary>
    /// Instalação já existente não tem essa coluna — ALTER TABLE ADD COLUMN
    /// simples resolve aqui (diferente do order_id acima, não precisa
    /// recriar a tabela: só estava faltando a coluna, não afrouxando uma
    /// restrição NOT NULL).
    /// </summary>
    private void AddTrackingCodeColumnIfNeeded()
    {
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "SELECT COUNT(*) FROM pragma_table_info('jobs') WHERE name = 'tracking_code';";
        var exists = Convert.ToInt64(pragma.ExecuteScalar() ?? 0L) == 1;

        if (exists)
        {
            return;
        }

        using var alter = _connection.CreateCommand();
        alter.CommandText = "ALTER TABLE jobs ADD COLUMN tracking_code TEXT NULL;";
        alter.ExecuteNonQuery();
    }

    /// <summary>
    /// Id de venda do canal (fallback pro nome do arquivo arquivado quando
    /// tracking_code ainda não existe, pedido 2026-08-09) — mesmo caso do
    /// AddTrackingCodeColumnIfNeeded acima, só ALTER TABLE simples.
    /// </summary>
    private void AddSaleIdColumnIfNeeded()
    {
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "SELECT COUNT(*) FROM pragma_table_info('jobs') WHERE name = 'sale_id';";
        var exists = Convert.ToInt64(pragma.ExecuteScalar() ?? 0L) == 1;

        if (exists)
        {
            return;
        }

        using var alter = _connection.CreateCommand();
        alter.CommandText = "ALTER TABLE jobs ADD COLUMN sale_id TEXT NULL;";
        alter.ExecuteNonQuery();
    }

    /// <summary>
    /// Instalação já existente criou a tabela com "order_id INTEGER NOT
    /// NULL" antes da etiqueta manual (sem pedido) existir — CREATE TABLE
    /// IF NOT EXISTS não corrige isso sozinho, e SQLite não suporta soltar
    /// NOT NULL direto, então reconstrói a tabela (padrão recomendado pelo
    /// próprio SQLite pra esse tipo de mudança). Fila local é só estado
    /// operacional recuperável via polling do servidor, então não tem
    /// problema em recriar.
    /// </summary>
    private void MigrateOrderIdToNullableIfNeeded()
    {
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "SELECT \"notnull\" FROM pragma_table_info('jobs') WHERE name = 'order_id';";
        var isNotNull = Convert.ToInt64(pragma.ExecuteScalar() ?? 0L) == 1;

        if (!isNotNull)
        {
            return;
        }

        using var migrate = _connection.CreateCommand();
        migrate.CommandText = """
            ALTER TABLE jobs RENAME TO jobs_old;

            CREATE TABLE jobs (
                server_job_id INTEGER PRIMARY KEY,
                order_id INTEGER NULL,
                channel TEXT NULL,
                shipping_type TEXT NULL,
                status TEXT NOT NULL,
                attempt_count INTEGER NOT NULL DEFAULT 0,
                last_error TEXT NULL,
                next_attempt_at TEXT NOT NULL,
                enqueued_at TEXT NOT NULL,
                printed_at TEXT NULL
            );

            INSERT INTO jobs SELECT * FROM jobs_old;

            DROP TABLE jobs_old;
            """;
        migrate.ExecuteNonQuery();
    }

    public Task UpsertAsync(QueuedJob job, CancellationToken ct = default)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO jobs (server_job_id, order_id, channel, shipping_type, tracking_code, sale_id, status, attempt_count, last_error, next_attempt_at, enqueued_at, printed_at)
            VALUES ($serverJobId, $orderId, $channel, $shippingType, $trackingCode, $saleId, $status, $attemptCount, $lastError, $nextAttemptAt, $enqueuedAt, $printedAt)
            ON CONFLICT(server_job_id) DO UPDATE SET
                channel = excluded.channel,
                shipping_type = excluded.shipping_type,
                tracking_code = excluded.tracking_code,
                sale_id = excluded.sale_id,
                status = excluded.status,
                attempt_count = excluded.attempt_count,
                last_error = excluded.last_error,
                next_attempt_at = excluded.next_attempt_at,
                printed_at = excluded.printed_at;
            """;

        command.Parameters.AddWithValue("$serverJobId", job.ServerJobId);
        command.Parameters.AddWithValue("$orderId", (object?) job.OrderId ?? DBNull.Value);
        command.Parameters.AddWithValue("$channel", (object?) job.Channel ?? DBNull.Value);
        command.Parameters.AddWithValue("$shippingType", (object?) job.ShippingType ?? DBNull.Value);
        command.Parameters.AddWithValue("$trackingCode", (object?) job.TrackingCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$saleId", (object?) job.SaleId ?? DBNull.Value);
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

    public Task<int> DeleteOldTerminalJobsAsync(DateTimeOffset olderThan, CancellationToken ct = default)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            DELETE FROM jobs
            WHERE status IN ($printed, $failedPermanently)
              AND COALESCE(printed_at, next_attempt_at) < $olderThan;
            """;
        command.Parameters.AddWithValue("$printed", QueuedJobStatus.Printed.ToString());
        command.Parameters.AddWithValue("$failedPermanently", QueuedJobStatus.FailedPermanently.ToString());
        command.Parameters.AddWithValue("$olderThan", olderThan.ToString("o"));

        return Task.FromResult(command.ExecuteNonQuery());
    }

    private static QueuedJob Map(SqliteDataReader reader)
    {
        return new QueuedJob
        {
            ServerJobId = reader.GetInt64(reader.GetOrdinal("server_job_id")),
            OrderId = reader.IsDBNull(reader.GetOrdinal("order_id")) ? null : reader.GetInt64(reader.GetOrdinal("order_id")),
            Channel = reader.IsDBNull(reader.GetOrdinal("channel")) ? null : reader.GetString(reader.GetOrdinal("channel")),
            ShippingType = reader.IsDBNull(reader.GetOrdinal("shipping_type")) ? null : reader.GetString(reader.GetOrdinal("shipping_type")),
            TrackingCode = reader.IsDBNull(reader.GetOrdinal("tracking_code")) ? null : reader.GetString(reader.GetOrdinal("tracking_code")),
            SaleId = reader.IsDBNull(reader.GetOrdinal("sale_id")) ? null : reader.GetString(reader.GetOrdinal("sale_id")),
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
