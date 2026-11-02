namespace VibePlayer.E2ETests;

/// <summary>Relógio falso que avança um passo fixo a cada leitura.</summary>
public sealed class SteppingTimeProvider(TimeSpan step) : TimeProvider
{
    private long _ticks = DateTimeOffset.UnixEpoch.UtcTicks;

    public override DateTimeOffset GetUtcNow() =>
        new(Interlocked.Add(ref _ticks, step.Ticks), TimeSpan.Zero);
}
