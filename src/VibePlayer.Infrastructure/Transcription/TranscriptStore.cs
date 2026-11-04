using Microsoft.EntityFrameworkCore;
using VibePlayer.Core.Transcription;
using VibePlayer.Infrastructure.Library;
using VibePlayer.Infrastructure.Persistence;

namespace VibePlayer.Infrastructure.Transcription;

/// <summary><see cref="ITranscriptStore"/> sobre as tabelas da spec 0007.</summary>
public sealed class TranscriptStore(IDbContextFactory<VibePlayerDbContext> contextFactory, TimeProvider timeProvider) : ITranscriptStore
{
    public async Task<LessonFile?> GetLessonFileAsync(Guid lessonId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var lesson = await db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lessonId, cancellationToken).ConfigureAwait(false);
        if (lesson is null)
        {
            return null;
        }

        var course = await db.Courses.AsNoTracking().Include(c => c.Root).FirstAsync(c => c.Id == lesson.CourseId, cancellationToken).ConfigureAwait(false);
        return new LessonFile(lesson.Id, CourseLibrary.FullPath(course, lesson), lesson.FileSize, lesson.LastWriteUtc, lesson.IsAvailable);
    }

    public async Task<LessonTranscriptionRecord?> GetRecordAsync(Guid lessonId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.LessonTranscriptions.AsNoTracking().FirstOrDefaultAsync(l => l.LessonId == lessonId, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task SaveRecordAsync(LessonTranscriptionRecord record, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.LessonTranscriptions.FirstOrDefaultAsync(l => l.LessonId == record.LessonId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            entity = new LessonTranscriptionEntity { LessonId = record.LessonId };
            db.LessonTranscriptions.Add(entity);
        }

        entity.Status = record.Status;
        entity.TranscriptId = record.TranscriptId;
        entity.VideoHash = record.VideoHash;
        entity.HashedFileSize = record.HashedFileSize;
        entity.HashedLastWriteUtc = record.HashedLastWriteUtc;
        entity.ErrorCode = record.ErrorCode;
        entity.QueuedAt = record.QueuedAt;
        entity.PreferWhisper = record.PreferWhisper;
        entity.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRecordAsync(Guid lessonId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.LessonTranscriptions.Where(l => l.LessonId == lessonId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LessonTranscriptionRecord>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entities = await db.LessonTranscriptions.AsNoTracking()
            .Where(l => l.Status == TranscriptionStatus.Queued || l.Status == TranscriptionStatus.Running)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Ordenado aqui porque o SQLite não ordena DateTimeOffset pelo EF.
        return [.. entities.OrderBy(entity => entity.QueuedAt).Select(ToRecord)];
    }

    public async Task<Transcript?> GetTranscriptAsync(Guid transcriptId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.Transcripts.AsNoTracking().Include(t => t.Segments).FirstOrDefaultAsync(t => t.Id == transcriptId, cancellationToken).ConfigureAwait(false);
        return entity is null
            ? null
            : new Transcript(
                entity.Id,
                entity.Source,
                entity.Model,
                entity.Language,
                [.. entity.Segments.OrderBy(s => s.Position).Select(s => new TranscriptSegment(TimeSpan.FromMilliseconds(s.StartMs), TimeSpan.FromMilliseconds(s.EndMs), s.Text))]);
    }

    public async Task<Guid?> FindTranscriptAsync(string sourceHash, TranscriptSource source, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var id = await db.Transcripts.AsNoTracking()
            .Where(t => t.SourceHash == sourceHash && t.Source == source)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return id;
    }

    public async Task SaveTranscriptAsync(string sourceHash, Transcript transcript, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Um SaveChanges só, que já é uma transação: a transcrição nunca fica pela metade.
        // Refazer pelo Whisper com outro modelo substitui a anterior do mesmo vídeo.
        await db.Transcripts.Where(t => t.SourceHash == sourceHash && t.Source == transcript.Source).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        db.Transcripts.Add(new TranscriptEntity
        {
            Id = transcript.Id,
            SourceHash = sourceHash,
            Source = transcript.Source,
            Model = transcript.Model,
            Language = transcript.Language,
            CreatedAt = timeProvider.GetUtcNow(),
            Segments =
            [
                .. transcript.Segments.Select((segment, index) => new TranscriptSegmentEntity
                {
                    TranscriptId = transcript.Id,
                    Position = index,
                    StartMs = (long)segment.Start.TotalMilliseconds,
                    EndMs = (long)segment.End.TotalMilliseconds,
                    Text = segment.Text,
                }),
            ],
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static LessonTranscriptionRecord ToRecord(LessonTranscriptionEntity entity) => new(
        entity.LessonId,
        entity.Status,
        entity.TranscriptId,
        entity.VideoHash,
        entity.HashedFileSize,
        entity.HashedLastWriteUtc,
        entity.ErrorCode,
        entity.QueuedAt,
        entity.PreferWhisper);
}
