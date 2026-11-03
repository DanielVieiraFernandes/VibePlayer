using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibePlayer.Core.Library;
using VibePlayer.Core.Playback;
using VibePlayer.Infrastructure.Library;
using VibePlayer.Infrastructure.Persistence;

namespace VibePlayer.Infrastructure.Playback;

/// <summary>Lê, em segundo plano, a duração das aulas que ainda não têm (ou cujo arquivo mudou).</summary>
public sealed class LessonDurationUpdater(
    IDbContextFactory<VibePlayerDbContext> contextFactory,
    IMediaInfoReader mediaInfo,
    ILogger<LessonDurationUpdater> logger) : ILessonDurationUpdater
{
    // Duas leituras ao mesmo tempo: rápido em SSD, sem afogar um HD externo.
    private const int Parallelism = 2;
    private const int BatchSize = 50;

    public async Task UpdateMissingAsync(Action? batchSaved = null, CancellationToken cancellationToken = default)
    {
        List<(Guid Id, string Path)> pending;
        await using (var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
        {
            var lessons = await db.Lessons.AsNoTracking()
                .Where(l => l.IsAvailable && l.DurationMs == null)
                .Join(db.Courses.AsNoTracking().Include(c => c.Root), l => l.CourseId, c => c.Id, (l, c) => new { Lesson = l, Course = c })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            pending = lessons.Select(x => (x.Lesson.Id, CourseLibrary.FullPath(x.Course, x.Lesson))).ToList();
        }

        if (pending.Count == 0)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var read = 0;
        foreach (var batch in pending.Chunk(BatchSize))
        {
            var results = new List<(Guid Id, TimeSpan Duration)>();
            await Parallel.ForEachAsync(
                batch,
                new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = cancellationToken },
                async (lesson, token) =>
                {
                    if (await mediaInfo.ReadDurationAsync(lesson.Path, token).ConfigureAwait(false) is { } duration)
                    {
                        lock (results)
                        {
                            results.Add((lesson.Id, duration));
                        }
                    }
                }).ConfigureAwait(false);

            // Um lote por transação: no SQLite, cada gravação fora de transação é um fsync.
            await using (var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
            await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (var (id, duration) in results)
                {
                    await db.Lessons.Where(l => l.Id == id)
                        .ExecuteUpdateAsync(set => set.SetProperty(l => l.DurationMs, (long)duration.TotalMilliseconds), cancellationToken)
                        .ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            read += results.Count;
            batchSaved?.Invoke();
        }

        logger.LogInformation("Durações lidas: {Count} de {Total} em {Elapsed}", read, pending.Count, stopwatch.Elapsed);
    }
}
