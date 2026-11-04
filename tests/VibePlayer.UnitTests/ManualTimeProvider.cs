namespace VibePlayer.UnitTests;

/// <summary>Relógio falso controlado pelo teste, em UTC para "hoje" não depender do fuso da máquina.</summary>
public sealed class ManualTimeProvider(DateTimeOffset? start = null) : TimeProvider
{
    private DateTimeOffset _now = start ?? DateTimeOffset.UnixEpoch;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
