namespace KazakoraAgent.Core.Tests;

/// <summary>Tempo controlado manualmente pra testar backoff sem Thread.Sleep de verdade.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public ManualTimeProvider(DateTimeOffset start) => _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
