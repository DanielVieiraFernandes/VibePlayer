using VibePlayer.Core.Settings;

namespace VibePlayer.Core.Transcription;

/// <summary>Um trecho de texto com seu tempo no vídeo.</summary>
public sealed record TranscriptSegment(TimeSpan Start, TimeSpan End, string Text);

public enum TranscriptSource
{
    Whisper = 0,
    Subtitle = 1,
}

/// <summary>Texto de uma aula. Vive por hash do arquivo de origem, e não por aula.</summary>
/// <param name="Id">Identidade da transcrição.</param>
/// <param name="Source">De onde veio o texto.</param>
/// <param name="Model">Nome do modelo do Whisper; nulo em legendas.</param>
/// <param name="Language">Idioma, quando conhecido.</param>
/// <param name="Segments">Trechos, em ordem de tempo.</param>
public sealed record Transcript(Guid Id, TranscriptSource Source, string? Model, string? Language, IReadOnlyList<TranscriptSegment> Segments);

public enum TranscriptionStatus
{
    NotTranscribed,
    Queued,
    Running,
    Done,
    Failed,
    NoAudio,
}

public enum TranscriptionStage
{
    /// <summary>Calculando o hash, antes de saber se já existe transcrição.</summary>
    Preparing,
    Extracting,
    Transcribing,
}

/// <summary>Códigos de falha, gravados no banco e mapeados para textos na interface.</summary>
public static class TranscriptionErrors
{
    public const string UnreadableFile = "unreadable-file";
    public const string FileMissing = "file-missing";
    public const string ModelMissing = "model-missing";
    public const string ModelInvalid = "model-invalid";
    public const string VlcUnavailable = "vlc-unavailable";
    public const string ProcessCrashed = "process-crashed";
    public const string Unexpected = "unexpected";
}

/// <summary>O que a interface mostra de uma aula.</summary>
/// <param name="LessonId">Aula.</param>
/// <param name="Status">Situação atual.</param>
/// <param name="ErrorCode">Um de <see cref="TranscriptionErrors"/>, quando <see cref="TranscriptionStatus.Failed"/>.</param>
/// <param name="Transcript">O texto, quando <see cref="TranscriptionStatus.Done"/>.</param>
public sealed record LessonTranscription(Guid LessonId, TranscriptionStatus Status, string? ErrorCode = null, Transcript? Transcript = null);

/// <summary>Progresso da aula em andamento.</summary>
public sealed record TranscriptionProgress(Guid LessonId, TranscriptionStage Stage, double Fraction);

/// <summary>Um modelo do Whisper que o app sabe baixar.</summary>
/// <param name="Name">Nome permanente, gravado nas preferências e nas transcrições.</param>
/// <param name="FileName">Arquivo no repositório de modelos e em <c>&lt;dados&gt;/models</c>.</param>
/// <param name="SizeBytes">Tamanho exato do arquivo.</param>
/// <param name="Sha256">Hash esperado, em minúsculas: um arquivo adulterado ou truncado não é usado.</param>
public sealed record WhisperModel(string Name, string FileName, long SizeBytes, string Sha256);

public static class WhisperModels
{
    /// <summary>Padrão: o melhor equilíbrio medido na spec 0007 (7,8x na CPU, 641 MB de memória).</summary>
    public static readonly WhisperModel SmallQuantized = new(
        "small-q5_1",
        "ggml-small-q5_1.bin",
        190_085_487,
        "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb");

    /// <summary>Opção "Mais preciso": cerca de cinco vezes mais lento na CPU.</summary>
    public static readonly WhisperModel TurboQuantized = new(
        "large-v3-turbo-q5_0",
        "ggml-large-v3-turbo-q5_0.bin",
        574_041_195,
        "394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2");

    public static IReadOnlyList<WhisperModel> All { get; } = [SmallQuantized, TurboQuantized];

    public static WhisperModel ByName(string name) =>
        All.FirstOrDefault(model => model.Name == name) ?? SmallQuantized;
}

public static class TranscriptionSettings
{
    /// <summary>"auto" deixa o Whisper detectar o idioma.</summary>
    public const string AutomaticLanguage = "auto";

    public static readonly SettingKey<string> Model = new(
        "transcription.model",
        WhisperModels.SmallQuantized.Name,
        name => WhisperModels.All.Any(model => model.Name == name));

    public static readonly SettingKey<string> Language = new(
        "transcription.language",
        AutomaticLanguage,
        language => language is AutomaticLanguage or "pt" or "en" or "es");

    /// <summary>Ligada depois de uma falha na GPU: driver ruim não pode virar falha em toda aula.</summary>
    public static readonly SettingKey<bool> ForceCpu = new("transcription.forceCpu", false);
}
