namespace VibePlayer.Core.Transcription;

/// <summary>Uma aula para o processo de transcrição.</summary>
public sealed record TranscriptionJob(string VideoPath, string ModelPath, string Language, int Threads, bool ForceCpu);

public enum WorkerEventKind
{
    /// <summary>Nova etapa, em <see cref="WorkerEvent.Stage"/>.</summary>
    Stage,

    /// <summary>Andamento da etapa, em <see cref="WorkerEvent.Fraction"/>.</summary>
    Progress,

    /// <summary>Runtime carregado (Vulkan, Cpu...), em <see cref="WorkerEvent.Value"/>.</summary>
    Runtime,

    /// <summary>Um trecho de texto.</summary>
    Segment,

    /// <summary>Aviso de que o áudio saiu com o reamostrador ruim do VLC.</summary>
    LowQualityAudio,

    NoAudio,

    /// <summary>Fim normal. <see cref="WorkerEvent.Value"/> traz o idioma.</summary>
    Done,

    /// <summary>Falha esperada, com um dos <see cref="TranscriptionErrors"/> em <see cref="WorkerEvent.Value"/>.</summary>
    Error,
}

/// <summary>
/// Mensagem do processo de transcrição: uma linha de JSON na saída padrão. Só o evento
/// <see cref="WorkerEventKind.Segment"/> tem texto da aula, e ele nunca vai para o log.
/// </summary>
public sealed record WorkerEvent(
    WorkerEventKind Kind,
    TranscriptionStage? Stage = null,
    double? Fraction = null,
    string? Value = null,
    long? StartMs = null,
    long? EndMs = null,
    string? Text = null);

/// <summary>O processo terminou sem avisar o fim: falha nativa, falta de memória ou morto pelo sistema.</summary>
public sealed class WorkerCrashedException(int exitCode) : Exception($"O processo de transcrição terminou com o código {exitCode}.")
{
    public int ExitCode { get; } = exitCode;
}

/// <summary>Roda um job em processo separado, para uma falha nativa não derrubar o app.</summary>
public interface ITranscriptionWorker
{
    /// <returns>Os eventos, até <see cref="WorkerEventKind.Done"/>, <see cref="WorkerEventKind.NoAudio"/> ou <see cref="WorkerEventKind.Error"/>.</returns>
    /// <exception cref="WorkerCrashedException">O processo acabou sem um desses eventos.</exception>
    IAsyncEnumerable<WorkerEvent> RunAsync(TranscriptionJob job, CancellationToken cancellationToken);
}

/// <summary>Modelos do Whisper em <c>&lt;dados&gt;/models</c>.</summary>
public interface IWhisperModelStore
{
    bool IsAvailable(WhisperModel model);

    string PathOf(WhisperModel model);

    /// <summary>Baixa o modelo, retomando um download interrompido.</summary>
    /// <param name="model">Modelo a baixar.</param>
    /// <param name="progress">Fração baixada, de 0 a 1.</param>
    /// <param name="cancellationToken">Interrompe e mantém o que já veio, para retomar depois.</param>
    /// <exception cref="ModelDownloadException">Sem espaço, rede fora ou arquivo com hash diferente.</exception>
    Task DownloadAsync(WhisperModel model, IProgress<double>? progress, CancellationToken cancellationToken);
}

public enum ModelDownloadError
{
    Network,
    NotEnoughSpace,
    HashMismatch,
}

public sealed class ModelDownloadException(ModelDownloadError error, Exception? inner = null)
    : Exception($"Falha ao baixar o modelo: {error}.", inner)
{
    public ModelDownloadError Error { get; } = error;
}

/// <summary>Procura e lê a legenda ao lado do vídeo.</summary>
public interface ISubtitleSource
{
    /// <returns>Caminho da melhor legenda, ou <c>null</c>.</returns>
    string? Find(string videoPath);

    Task<byte[]> ReadAsync(string subtitlePath, CancellationToken cancellationToken = default);
}

public interface IFileHasher
{
    /// <returns>SHA-256 do arquivo, em hexadecimal minúsculo.</returns>
    Task<string> ComputeAsync(string path, CancellationToken cancellationToken = default);
}
