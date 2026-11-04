namespace VibePlayer.Infrastructure.Transcription.Worker;

public enum AudioExtractionStatus
{
    Completed,

    /// <summary>O arquivo abre, mas não tem faixa de áudio.</summary>
    NoAudio,

    /// <summary>O arquivo não abre ou nenhum áudio saiu dele.</summary>
    Unreadable,
}

/// <param name="Status">Como a extração terminou.</param>
/// <param name="Samples">PCM em 16 kHz, mono, de -1 a 1; vazio se o status não for <see cref="AudioExtractionStatus.Completed"/>.</param>
/// <param name="LowQualityResampler">O VLC não tinha o speex_resampler: os tempos seguem certos, mas o áudio piora.</param>
public sealed record AudioExtractionResult(AudioExtractionStatus Status, ReadOnlyMemory<float> Samples, bool LowQualityResampler = false);

/// <summary>
/// Extrai o áudio de um arquivo no formato do Whisper. É interface porque uma versão futura do VLC
/// pode mudar o smem, e outro extrator entraria aqui.
/// </summary>
public interface IAudioExtractor
{
    /// <param name="path">Arquivo de vídeo.</param>
    /// <param name="progress">Fração do áudio já extraída, de 0 a 1.</param>
    /// <param name="cancellationToken">Interrompe a extração.</param>
    Task<AudioExtractionResult> ExtractAsync(string path, IProgress<double>? progress, CancellationToken cancellationToken);
}
