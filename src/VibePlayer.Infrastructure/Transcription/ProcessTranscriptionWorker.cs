using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using VibePlayer.Core.Transcription;

namespace VibePlayer.Infrastructure.Transcription;

/// <summary>Como abrir o processo de transcrição. Separado para os testes usarem outro programa.</summary>
public sealed record TranscriptionWorkerCommand(string FileName, IReadOnlyList<string> Arguments)
{
    /// <summary>O próprio executável do app, para não mudar o empacotamento (spec 0007).</summary>
    public static TranscriptionWorkerCommand CurrentProcess() =>
        new(Environment.ProcessPath ?? throw new InvalidOperationException("Caminho do executável desconhecido."), [WorkerProtocol.Argument]);
}

/// <summary>Abre o processo de transcrição, repassa os eventos e cancela matando o processo.</summary>
public sealed class ProcessTranscriptionWorker(TranscriptionWorkerCommand command) : ITranscriptionWorker
{
    // Depois do evento final, o processo só precisa fechar o Whisper; mais que isso é travamento.
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(5);

    public async IAsyncEnumerable<WorkerEvent> RunAsync(TranscriptionJob job, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = CreateStartInfo() };
        process.Start();
        try
        {
            SetLowPriority(process);

            // O stderr é descartado sem ler o conteúdo, que não é controlado pelo app e não pode ir
            // para o log; mas precisa ser drenado, senão o processo trava com o pipe cheio.
            process.BeginErrorReadLine();

            // A entrada fica aberta até o fim: fechá-la é o aviso de que o app morreu.
            await process.StandardInput.WriteLineAsync(WorkerProtocol.Serialize(job).AsMemory(), cancellationToken).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);

            var finished = false;
            while (!finished && await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (WorkerProtocol.TryParseEvent(line) is { } workerEvent)
                {
                    finished = workerEvent.Kind is WorkerEventKind.Done or WorkerEventKind.NoAudio or WorkerEventKind.Error;
                    yield return workerEvent;
                }
            }

            if (!finished)
            {
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                throw new WorkerCrashedException(process.ExitCode);
            }

            await WaitForExitAsync(process).ConfigureAwait(false);
        }
        finally
        {
            KillIfRunning(process);
        }
    }

    // O resultado já chegou: um processo lento para sair não pode virar falha da transcrição. Quem
    // ainda estiver vivo depois do tempo limite é encerrado pelo finally de RunAsync.
    private static async Task WaitForExitAsync(Process process)
    {
        using var exit = new CancellationTokenSource(ExitTimeout);
        try
        {
            await process.WaitForExitAsync(exit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private ProcessStartInfo CreateStartInfo()
    {
        var startInfo = new ProcessStartInfo(command.FileName)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    // Falhar aqui não impede a transcrição: só deixa o processo na prioridade normal.
    private static void SetLowPriority(Process process)
    {
        try
        {
            process.PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
        }
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // O processo terminou entre a verificação e o Kill.
        }
    }
}
