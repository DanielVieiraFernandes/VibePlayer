using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;

namespace VibePlayer.App.Services;

/// <summary>
/// Notificações sobre o <see cref="WindowNotificationManager"/> do Avalonia, presas à janela
/// principal por <see cref="AttachTo"/>.
/// </summary>
public sealed class NotificationService(TimeProvider timeProvider) : INotificationService
{
    /// <summary>
    /// Uma falha em loop (ex.: binding quebrado a cada redesenho) geraria dezenas de avisos por
    /// segundo e cobriria a tela; um a cada 5 s basta para o usuário saber que algo deu errado.
    /// </summary>
    public static readonly TimeSpan ErrorInterval = TimeSpan.FromSeconds(5);

    private readonly NotificationThrottle _errorThrottle = new(timeProvider, ErrorInterval);
    private WindowNotificationManager? _manager;

    public void AttachTo(TopLevel host)
    {
        _manager = new WindowNotificationManager(host)
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = 3,
        };
    }

    public void ShowError(string title, string message)
    {
        if (!_errorThrottle.TryEnter())
        {
            return;
        }

        // Post, e não chamada direta: quem avisa pode estar fora da thread de UI (finalizer do GC).
        Dispatcher.UIThread.Post(() => _manager?.Show(new Notification(title, message, NotificationType.Error)));
    }
}
