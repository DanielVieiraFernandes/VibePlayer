using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Serilog.Events;
using VibePlayer.App;
using VibePlayer.App.Resources;

namespace VibePlayer.E2ETests;

public sealed class UnhandledExceptionTests
{
    [AvaloniaFact]
    public void ExcecaoEmComandoDaUi_MostraNotificacaoERegistraErro()
    {
        var app = (VibePlayerApp)Application.Current!;
        var window = app.CreateMainWindow();
        window.Show();
        var marker = Guid.NewGuid().ToString();

        // Simula um comando/evento de UI que lança. Sem o handler global, RunJobs relançaria
        // a exceção e este teste falharia — o que equivale ao app fechando.
        Dispatcher.UIThread.Post(() => throw new InvalidOperationException(marker));
        Dispatcher.UIThread.RunJobs();

        TestAppBuilder.LogSink.Events.ShouldContain(e =>
            e.Level == LogEventLevel.Error && e.Exception != null && e.Exception.Message == marker);
        window.IsVisible.ShouldBeTrue();

        WindowTestExtensions.Settle();
        window.GetVisualDescendants().OfType<NotificationCard>()
            .Select(card => card.Content)
            .OfType<INotification>()
            .ShouldContain(n => n.Type == NotificationType.Error && n.Title == Strings.Error_Title);

        window.Close();
    }

    [AvaloniaFact]
    public void TaskNaoObservada_RegistraErroSemEncerrar()
    {
        var marker = Guid.NewGuid().ToString();

        CreateUnobservedFaultedTask(marker);

        // O .NET só percebe que ninguém observou a exceção quando o GC finaliza a task;
        // forçar a coleta aqui torna o cenário determinístico.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        TestAppBuilder.LogSink.Events.ShouldContain(e =>
            e.Level == LogEventLevel.Error
            && e.Exception is AggregateException
            && ((AggregateException)e.Exception).InnerExceptions.Any(inner => inner.Message == marker));
    }

    // Método separado e sem inlining para que nenhuma referência à task sobreviva no método
    // de teste; caso contrário o GC não a coletaria e o evento nunca seria disparado.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateUnobservedFaultedTask(string marker) =>
        _ = Task.FromException(new InvalidOperationException(marker));
}
