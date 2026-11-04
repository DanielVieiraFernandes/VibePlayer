namespace VibePlayer.Core.Transcription;

/// <summary>Arquivo de uma aula, como a transcrição precisa vê-lo.</summary>
public sealed record LessonFile(Guid LessonId, string Path, long FileSize, DateTime LastWriteUtc, bool IsAvailable);

/// <summary>Vínculo salvo entre uma aula e sua transcrição.</summary>
/// <param name="LessonId">Aula.</param>
/// <param name="Status">Situação salva.</param>
/// <param name="TranscriptId">Transcrição em uso.</param>
/// <param name="VideoHash">Hash do vídeo, quando já calculado.</param>
/// <param name="HashedFileSize">Tamanho do vídeo quando o hash foi calculado.</param>
/// <param name="HashedLastWriteUtc">Data do vídeo quando o hash foi calculado.</param>
/// <param name="ErrorCode">Um de <see cref="TranscriptionErrors"/>.</param>
/// <param name="QueuedAt">Quando entrou na fila.</param>
/// <param name="PreferWhisper">O usuário escolheu o Whisper apesar de existir legenda.</param>
public sealed record LessonTranscriptionRecord(
    Guid LessonId,
    TranscriptionStatus Status,
    Guid? TranscriptId = null,
    string? VideoHash = null,
    long? HashedFileSize = null,
    DateTime? HashedLastWriteUtc = null,
    string? ErrorCode = null,
    DateTimeOffset? QueuedAt = null,
    bool PreferWhisper = false);

/// <summary>Persistência da transcrição. Transcrições são por hash; o vínculo é por aula.</summary>
public interface ITranscriptStore
{
    Task<LessonFile?> GetLessonFileAsync(Guid lessonId, CancellationToken cancellationToken = default);

    Task<LessonTranscriptionRecord?> GetRecordAsync(Guid lessonId, CancellationToken cancellationToken = default);

    /// <summary>Cria ou substitui o vínculo da aula.</summary>
    Task SaveRecordAsync(LessonTranscriptionRecord record, CancellationToken cancellationToken = default);

    /// <summary>Volta a aula a "Não transcrita". Transcrições salvas ficam para o cache.</summary>
    Task DeleteRecordAsync(Guid lessonId, CancellationToken cancellationToken = default);

    /// <summary>Aulas com fila pendente, da mais antiga para a mais nova.</summary>
    Task<IReadOnlyList<LessonTranscriptionRecord>> GetPendingAsync(CancellationToken cancellationToken = default);

    Task<Transcript?> GetTranscriptAsync(Guid transcriptId, CancellationToken cancellationToken = default);

    /// <summary>Transcrição existente para um arquivo de origem, sem carregar os segmentos.</summary>
    Task<Guid?> FindTranscriptAsync(string sourceHash, TranscriptSource source, CancellationToken cancellationToken = default);

    /// <summary>Grava a transcrição inteira numa transação, para nunca ficar pela metade.</summary>
    Task SaveTranscriptAsync(string sourceHash, Transcript transcript, CancellationToken cancellationToken = default);
}
