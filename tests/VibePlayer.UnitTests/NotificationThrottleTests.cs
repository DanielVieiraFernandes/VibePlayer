using VibePlayer.App.Services;

namespace VibePlayer.UnitTests;

public sealed class NotificationThrottleTests
{
    [Fact]
    public void ErrosRepetidos_SoUmPassaPorIntervalo()
    {
        var clock = new ManualTimeProvider();
        var throttle = new NotificationThrottle(clock, TimeSpan.FromSeconds(5));

        throttle.TryEnter().ShouldBeTrue();
        throttle.TryEnter().ShouldBeFalse();

        clock.Advance(TimeSpan.FromSeconds(4.9));
        throttle.TryEnter().ShouldBeFalse();

        clock.Advance(TimeSpan.FromSeconds(0.1));
        throttle.TryEnter().ShouldBeTrue();
    }
}
