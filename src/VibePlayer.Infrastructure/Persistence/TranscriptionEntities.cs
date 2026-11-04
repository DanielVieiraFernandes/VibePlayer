using VibePlayer.Core.Transcription;

namespace VibePlayer.Infrastructure.Persistence;

// Entidades da transcrição (spec 0007). A transcrição é por hash do arquivo de origem, e não por
// aula: o mesmo vídeo em dois cursos, ou uma pasta movida, reaproveitam o resultado. O que é da
// aula fica em LessonTranscriptionEntity, ligada a LessonEntity.Id.

public sealed class TranscriptEntity
{
    public Guid Id { get; set; }

    /// <summary>SHA-256 do vídeo (Whisper) ou do arquivo de legenda.</summary>
    public required string SourceHash { get; set; }

    public TranscriptSource Source { get; set; }

    public string? Model { get; set; }

    public string? Language { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<TranscriptSegmentEntity> Segments { get; set; } = [];
}

public sealed class TranscriptSegmentEntity
{
    public Guid TranscriptId { get; set; }

    public int Position { get; set; }

    public long StartMs { get; set; }

    public long EndMs { get; set; }

    public required string Text { get; set; }
}

/// <summary>Situação da transcrição de uma aula. Aula sem linha aqui é "Não transcrita".</summary>
public sealed class LessonTranscriptionEntity
{
    public Guid LessonId { get; set; }

    public TranscriptionStatus Status { get; set; }

    public Guid? TranscriptId { get; set; }

    public string? VideoHash { get; set; }

    public long? HashedFileSize { get; set; }

    public DateTime? HashedLastWriteUtc { get; set; }

    public string? ErrorCode { get; set; }

    public DateTimeOffset? QueuedAt { get; set; }

    public bool PreferWhisper { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
