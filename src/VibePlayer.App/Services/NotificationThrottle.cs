namespace VibePlayer.App.Services;

/// <summary>
/// Deixa passar no máximo um evento por intervalo. Separado do serviço de notificação para ser
/// testado sem UI e com relógio controlado.
/// </summary>
public sealed class NotificationThrottle(TimeProvider timeProvider, TimeSpan interval)
{
    private readonly Lock _lock = new();
    private DateTimeOffset? _last;

    public bool TryEnter()
    {
        // Lock porque a exceção de task não observada chega pela thread do finalizer do GC,
        // concorrendo com as da thread de UI.
        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();
            if (_last is { } last && now - last < interval)
            {
                return false;
            }

            _last = now;
            return true;
        }
    }
}
