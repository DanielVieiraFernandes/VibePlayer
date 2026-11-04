using System.Diagnostics;
using LibVLCSharp.Shared;
using Whisper.net;
using VibePlayer.Core.Transcription;

namespace VibePlayer.Infrastructure.Transcription.Worker;

/// <summary>
/// Entrada do processo de transcrição: o mesmo executável do app, com
/// <see cref="WorkerProtocol.Argument"/>, sem banco, sem interface e sem arquivo de log.
/// </summary>
/// <remarks>
/// Por que um processo separado (design da spec 0007): o .NET não recupera uma falha nativa. Um
/// abort no whisper.cpp, um driver de GPU ruim ou um arquivo que quebra o VLC fechariam o app no
/// meio de uma aula. Separado, isso vira só uma aula com falha. De quebra, a memória do modelo
/// volta ao sistema ao terminar, cancelar é só matar o processo, e a prioridade baixa vale para
/// todas as threads nativas.
///
/// Nada aqui grava log: quem registra é o app, com os metadados que chegam pelos eventos.
/// </remarks>
public sealed class WorkerEntryPoint(IAudioExtractor extractor, ISpeechTranscriber transcriber)
{
    private readonly Lock _outputLock = new();

    /// <summary>Roda o job lido da entrada padrão.</summary>
    public static int Main()
    {
        using var input = new StreamReader(Console.OpenStandardInput());
        using var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        var worker = new WorkerEntryPoint(new VlcAudioExtractor(), new WhisperTranscriber());
        return worker.RunAsync(input, output, onInputClosed: KillSelf, CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task<int> RunAsync(TextReader input, TextWriter output, Action onInputClosed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        var line = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (line is null || WorkerProtocol.ParseJob(line) is not { } job)
        {
            return 2;
        }

        // Task solta de propósito: ela só vigia a entrada e termina junto com o processo.
        _ = WatchInputAsync(input, onInputClosed);
        await RunJobAsync(job, output, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private async Task RunJobAsync(TranscriptionJob job, TextWriter output, CancellationToken cancellationToken)
    {
        void Send(WorkerEvent workerEvent)
        {
            lock (_outputLock)
            {
                output.WriteLine(WorkerProtocol.Serialize(workerEvent));
            }
        }

        try
        {
            Send(new WorkerEvent(WorkerEventKind.Stage, TranscriptionStage.Extracting));
            var progress = new CallbackProgress(fraction => Send(new WorkerEvent(WorkerEventKind.Progress, Fraction: fraction)));
            var audio = await extractor.ExtractAsync(job.VideoPath, progress, cancellationToken).ConfigureAwait(false);
            switch (audio.Status)
            {
                case AudioExtractionStatus.NoAudio:
                    Send(new WorkerEvent(WorkerEventKind.NoAudio));
                    return;
                case AudioExtractionStatus.Unreadable:
                    Send(new WorkerEvent(WorkerEventKind.Error, Value: TranscriptionErrors.UnreadableFile));
                    return;
            }

            if (audio.LowQualityResampler)
            {
                Send(new WorkerEvent(WorkerEventKind.LowQualityAudio));
            }

            Send(new WorkerEvent(WorkerEventKind.Stage, TranscriptionStage.Transcribing));
            var language = await transcriber.TranscribeAsync(
                audio.Samples,
                job,
                runtime => Send(new WorkerEvent(WorkerEventKind.Runtime, Value: runtime)),
                fraction => Send(new WorkerEvent(WorkerEventKind.Progress, Fraction: fraction)),
                segment => Send(new WorkerEvent(
                    WorkerEventKind.Segment,
                    StartMs: (long)segment.Start.TotalMilliseconds,
                    EndMs: (long)segment.End.TotalMilliseconds,
                    Text: segment.Text)),
                cancellationToken).ConfigureAwait(false);
            Send(new WorkerEvent(WorkerEventKind.Done, Value: language));
        }
        catch (Exception ex) when (ex is VLCException or DllNotFoundException or TypeInitializationException)
        {
            Send(new WorkerEvent(WorkerEventKind.Error, Value: TranscriptionErrors.VlcUnavailable));
        }
        catch (WhisperModelLoadException)
        {
            Send(new WorkerEvent(WorkerEventKind.Error, Value: TranscriptionErrors.ModelInvalid));
        }
        catch (TimeoutException)
        {
            Send(new WorkerEvent(WorkerEventKind.Error, Value: TranscriptionErrors.UnreadableFile));
        }
    }

    private static async Task WatchInputAsync(TextReader input, Action onInputClosed)
    {
        try
        {
            while (await input.ReadLineAsync().ConfigureAwait(false) is not null)
            {
            }
        }
        catch (IOException)
        {
            // Entrada quebrada é o mesmo que fechada: o app não está mais lá.
        }

        onInputClosed();
    }

    // Kill, e não Environment.Exit: Exit espera finalizadores e pode travar com threads nativas do
    // VLC e do Whisper ainda rodando. Não há nada a preservar aqui.
    private static void KillSelf()
    {
        using var self = Process.GetCurrentProcess();
        self.Kill();
    }

    private sealed class CallbackProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
