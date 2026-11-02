using Microsoft.Extensions.Logging;

namespace VibePlayer.Core.Threading;

public static class TaskExtensions
{
    /// <summary>
    /// Deixa uma tarefa rodando em segundo plano sem aguardá-la, garantindo que uma falha
    /// seja registrada no log em vez de derrubar o app ou sumir em silêncio.
    /// </summary>
    /// <remarks>
    /// Este é o único jeito permitido de disparar trabalho sem <c>await</c> no projeto.
    /// Um <c>Task.Run</c> solto ou um <c>async void</c> fora de evento de UI deixa a exceção
    /// sem dono: no melhor caso ela se perde, no pior o .NET encerra o processo — e
    /// <c>AppDomain.UnhandledException</c> só permite registrar, não impedir o encerramento.
    /// Aqui a tarefa sempre é observada, então isso não acontece.
    /// </remarks>
    /// <param name="operationName">Nome curto da operação, usado no log para identificar a origem do erro.</param>
    public static void FireAndForget(this Task task, ILogger logger, string operationName)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(logger);

        // ObserveAsync nunca lança, por isso descartar a Task dela é seguro.
        _ = ObserveAsync(task, logger, operationName);
    }

    private static async Task ObserveAsync(Task task, ILogger logger, string operationName)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancelamento é fluxo normal (usuário trocou de aula, fechou o app), não erro.
            logger.LogDebug("Operação em segundo plano {Operation} cancelada", operationName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha na operação em segundo plano {Operation}", operationName);
        }
    }
}
