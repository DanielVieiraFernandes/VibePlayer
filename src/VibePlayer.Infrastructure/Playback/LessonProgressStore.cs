using Microsoft.EntityFrameworkCore;
using VibePlayer.Core.Library;
using VibePlayer.Core.Playback;
using VibePlayer.Infrastructure.Library;
using VibePlayer.Infrastructure.Persistence;

namespace VibePlayer.Infrastructure.Playback;

/// <summary><see cref="ILessonProgressStore"/> sobre a tabela <c>LessonProgress</c>.</summary>
public sealed class LessonProgressStore(IDbContextFactory<VibePlayerDbContext> contextFactory, TimeProvider timeProvider)
    : ILessonProgressStore
{
    public async Task<LessonProgress?> GetAsync(Guid lessonId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entry = await db.LessonProgress.AsNoTracking().FirstOrDefaultAsync(p => p.LessonId == lessonId, cancellationToken).ConfigureAwait(false);
        return entry is null ? null : new LessonProgress(entry.LessonId, TimeSpan.FromMilliseconds(entry.PositionMs), entry.IsCompleted, entry.LastWatchedAt);
    }

    public Task SavePositionAsync(Guid lessonId, TimeSpan position, CancellationToken cancellationToken = default) =>
        UpdateAsync(lessonId, entry => entry.PositionMs = (long)position.TotalMilliseconds, cancellationToken);

    public Task SetCompletedAsync(Guid lessonId, bool completed, CancellationToken cancellationToken = default) =>
        UpdateAsync(
            lessonId,
            entry =>
            {
                if (entry.IsCompleted == completed)
                {
                    return;
                }

                entry.IsCompleted = completed;
                entry.CompletedAt = completed ? timeProvider.GetUtcNow() : null;

                // Desmarcar começa a aula de novo do início; marcar não mexe na posição.
                if (!completed)
                {
                    entry.PositionMs = 0;
                }
            },
            cancellationToken);

    public async Task<PlayableLesson?> GetPlayableAsync(Guid lessonId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var lesson = await db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lessonId, cancellationToken).ConfigureAwait(false);
        if (lesson is not { IsAvailable: true })
        {
            return null;
        }

        var course = await db.Courses.AsNoTracking().Include(c => c.Root).FirstAsync(c => c.Id == lesson.CourseId, cancellationToken).ConfigureAwait(false);
        return new PlayableLesson(
            lesson.Id,
            course.Id,
            lesson.Title,
            course.Title,
            CourseLibrary.FullPath(course, lesson),
            lesson.DurationMs is { } ms ? TimeSpan.FromMilliseconds(ms) : null);
    }

    public async Task<IReadOnlyList<(Guid Id, bool IsAvailable)>> GetCourseOrderAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var lessons = await db.Lessons.AsNoTracking()
            .Where(l => l.CourseId == courseId)
            .OrderBy(l => l.Position)
            .Select(l => new { l.Id, l.IsAvailable })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return lessons.Select(l => (l.Id, l.IsAvailable)).ToList();
    }

    public async Task SaveDurationAsync(Guid lessonId, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Lessons.Where(l => l.Id == lessonId)
            .ExecuteUpdateAsync(set => set.SetProperty(l => l.DurationMs, (long)duration.TotalMilliseconds), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task UpdateAsync(Guid lessonId, Action<LessonProgressEntity> change, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entry = await db.LessonProgress.FirstOrDefaultAsync(p => p.LessonId == lessonId, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            // A aula pode ter saído da biblioteca enquanto tocava; aí não há o que salvar.
            if (!await db.Lessons.AnyAsync(l => l.Id == lessonId, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            entry = new LessonProgressEntity { LessonId = lessonId };
            db.LessonProgress.Add(entry);
        }

        change(entry);
        entry.LastWatchedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
