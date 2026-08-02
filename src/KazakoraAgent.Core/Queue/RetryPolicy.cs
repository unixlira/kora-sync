namespace KazakoraAgent.Core.Queue;

/// <summary>
/// 10s, 30s, 60s, 120s, 300s — até 5 tentativas, conforme especificado.
/// A tentativa 1 é a primeira execução (delay zero); os delays abaixo valem
/// pra tentativa 2 em diante, indexados por (tentativa - 1).
/// </summary>
public sealed class RetryPolicy
{
    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(120),
        TimeSpan.FromSeconds(300),
    ];

    public int MaxAttempts => Delays.Length;

    public bool ShouldRetry(int attemptCount) => attemptCount < MaxAttempts;

    /// <param name="attemptCount">Quantas tentativas já foram feitas (a que acabou de falhar).</param>
    public TimeSpan DelayForNextAttempt(int attemptCount)
    {
        if (attemptCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptCount), "Precisa ter havido ao menos 1 tentativa.");
        }

        var index = Math.Min(attemptCount, Delays.Length) - 1;

        return Delays[index];
    }
}
