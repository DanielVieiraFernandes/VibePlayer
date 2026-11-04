using Whisper.net;
using Whisper.net.LibraryLoader;
using VibePlayer.Core.Transcription;

namespace VibePlayer.Infrastructure.Transcription.Worker;

/// <summary>Transforma áudio em texto. Interface porque os testes não carregam o Whisper.</summary>
public interface ISpeechTranscriber
{
    /// <param name="samples">PCM em 16 kHz, mono, de -1 a 1.</param>
    /// <param name="job">Modelo, idioma, threads e uso de GPU.</param>
    /// <param name="onRuntime">Recebe o runtime carregado, antes de transcrever.</param>
    /// <param name="onProgress">Fração transcrita, de 0 a 1.</param>
    /// <param name="onSegment">Recebe cada trecho.</param>
    /// <param name="cancellationToken">Interrompe a transcrição.</param>
    /// <returns>O idioma usado.</returns>
    Task<string?> TranscribeAsync(
        ReadOnlyMemory<float> samples,
        TranscriptionJob job,
        Action<string> onRuntime,
        Action<double> onProgress,
        Action<TranscriptSegment> onSegment,
        CancellationToken cancellationToken);
}

/// <summary>Whisper local, por <c>Whisper.net</c>. Roda só no processo de transcrição.</summary>
/// <remarks>
/// A ordem de runtime tenta a GPU e cai para a CPU quando o Whisper.net não encontra Vulkan. Isso
/// cobre a falta da biblioteca, mas não um driver que falha no meio: esse caso derruba o processo,
/// e o <see cref="TranscriptionService"/> repete na CPU. No macOS Apple Silicon o Metal vem no
/// runtime de CPU, por isso desligar a GPU exige também <c>UseGpu = false</c>.
/// </remarks>
public sealed class WhisperTranscriber : ISpeechTranscriber
{
    public async Task<string?> TranscribeAsync(
        ReadOnlyMemory<float> samples,
        TranscriptionJob job,
        Action<string> onRuntime,
        Action<double> onProgress,
        Action<TranscriptSegment> onSegment,
        CancellationToken cancellationToken)
    {
        RuntimeOptions.RuntimeLibraryOrder = job.ForceCpu
            ? [RuntimeLibrary.Cpu, RuntimeLibrary.CpuNoAvx]
            : [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu, RuntimeLibrary.CpuNoAvx];

        using var factory = WhisperFactory.FromPath(job.ModelPath, new WhisperFactoryOptions { UseGpu = !job.ForceCpu });
        using var processor = factory.CreateBuilder()
            .WithLanguage(job.Language)
            .WithThreads(job.Threads)
            .WithProgressHandler(percent => onProgress(percent / 100d))
            .Build();
        onRuntime(RuntimeOptions.LoadedLibrary?.ToString() ?? "Desconhecido");

        string? language = null;
        await foreach (var segment in processor.ProcessAsync(samples, cancellationToken).ConfigureAwait(false))
        {
            language = segment.Language;
            onSegment(new TranscriptSegment(segment.Start, segment.End, segment.Text));
        }

        return language;
    }
}
