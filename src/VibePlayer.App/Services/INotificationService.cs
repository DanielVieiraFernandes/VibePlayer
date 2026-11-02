namespace VibePlayer.App.Services;

/// <summary>Avisos não bloqueantes no canto inferior direito da janela principal.</summary>
public interface INotificationService
{
    /// <summary>
    /// Mostra um aviso de erro. Pode ser chamado de qualquer thread; avisos repetidos em menos
    /// de <see cref="NotificationService.ErrorInterval"/> são descartados (o log continua com todos).
    /// </summary>
    void ShowError(string title, string message);
}
