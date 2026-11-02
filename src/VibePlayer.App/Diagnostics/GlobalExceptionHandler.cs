using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace VibePlayer.App.Diagnostics;

/// <summary>
/// Rede de segurança para exceções que escaparam do código do app: registra no log e
/// mantém o app aberto sempre que o .NET permite.
/// </summary>
/// <remarks>
/// Só dois pontos de captura global permitem continuar rodando, e são os tratados aqui:
/// a thread de UI (eventos, comandos, bindings) e tasks cujas exceções ninguém observou.
/// Exceções em threads próprias ou em <c>async void</c> fora da UI não têm volta — o .NET
/// encerra o processo; elas são registradas como Fatal em <c>Program</c>, e o projeto evita
/// esses padrões por regra (segundo plano sempre via <c>FireAndForget</c>).
///
/// Não é fluxo normal de erro: comandos tratam suas falhas esperadas. Chegar aqui significa
/// um bug, por isso o registro é Error com stack trace completo.
/// </remarks>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
{
    private Dispatcher? _attachedDispatcher;
    private bool _attachedToTaskScheduler;

    public void Attach(Dispatcher dispatcher)
    {
        // A trava é por dispatcher, não global: o Avalonia pode recriar o dispatcher da UI
        // (os testes headless fazem isso a cada teste) e cada instância nova precisa do handler.
        // Em produção há um só dispatcher, então isto roda uma vez.
        if (!ReferenceEquals(_attachedDispatcher, dispatcher))
        {
            dispatcher.UnhandledException += OnDispatcherUnhandledException;
            _attachedDispatcher = dispatcher;
        }

        // Já o TaskScheduler é estático e vive o processo inteiro; anexar de novo duplicaria
        // cada registro no log.
        if (!_attachedToTaskScheduler)
        {
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            _attachedToTaskScheduler = true;
        }

        logger.LogDebug("Handlers globais de exceção anexados");
    }

    private void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        logger.LogError(e.Exception, "Exceção não tratada na thread de UI; o app continua em execução");
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        logger.LogError(e.Exception, "Exceção não observada em Task; o app continua em execução");

        // Marca como observada para que nenhuma configuração do runtime
        // (ThrowUnobservedTaskExceptions) transforme isso em encerramento do processo.
        e.SetObserved();
    }
}
