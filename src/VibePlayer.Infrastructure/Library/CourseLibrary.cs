using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibePlayer.Core.Library;
using VibePlayer.Infrastructure.Persistence;

namespace VibePlayer.Infrastructure.Library;

/// <summary>
/// <see cref="ICourseLibrary"/> sobre o banco, sincronizando com as pastas pelo caminho relativo.
/// </summary>
/// <remarks>
/// Sincronização de uma pasta adicionada (raiz):
/// <list type="number">
/// <item>Inacessível (HD desconectado) → ela e tudo dela ficam indisponíveis; nada é lido nem apagado.</item>
/// <item>Acessível → scan completo e junção com o banco pelo caminho relativo: curso pelo caminho
/// relativo à raiz, módulo e aula pelo caminho relativo ao curso. Novo entra com Id novo; existente
/// é atualizado e volta a ficar disponível; ausente fica indisponível com o mesmo Id.</item>
/// <item>Uma raiz por SaveChanges (uma transação): a UI nunca vê a biblioteca pela metade, e
/// cancelar no meio descarta só a raiz em andamento.</item>
/// </list>
/// Cursos de vídeos avulsos não têm raiz: cada aula guarda o caminho absoluto e é conferida
/// arquivo a arquivo.
/// </remarks>
public sealed class CourseLibrary(
    IDbContextFactory<VibePlayerDbContext> contextFactory,
    IFileSystemReader fileSystem,
    TimeProvider timeProvider,
    ILogger<CourseLibrary> logger) : ICourseLibrary, IDisposable
{
    private const int ProgressStep = 100;

    private readonly LibraryScanner _scanner = new(fileSystem);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Lock _refreshLock = new();
    private Task? _currentRefresh;

    public void Dispose() => _writeLock.Dispose();

    public async Task<IReadOnlyList<CourseNode>> GetCoursesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var courses = await db.Courses.AsNoTracking()
            .Include(c => c.Root)
            .Include(c => c.Modules)
            .Include(c => c.Lessons)
            .AsSplitQuery()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Ordem natural por título; "Vídeos avulsos" sempre por último.
        return courses
            .OrderBy(c => c.IsLooseVideos)
            .ThenBy(c => c.Title, NaturalStringComparer.Instance)
            .Select(ToNode)
            .ToList();
    }

    public async Task<LibraryKnownPaths> GetKnownPathsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var roots = await db.LibraryRoots.Select(r => r.Path).ToListAsync(cancellationToken).ConfigureAwait(false);
        var files = await db.Lessons
            .Where(l => db.Courses.Any(c => c.Id == l.CourseId && c.Kind == CourseKind.Files))
            .Select(l => l.Path)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new LibraryKnownPaths(roots, files);
    }

    public Task<FolderPreview> PreviewFolderAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => _scanner.Preview(LibraryPaths.NormalizeFull(path)), cancellationToken);

    public Task AddFolderAsync(string path, LibraryRootKind kind, CancellationToken cancellationToken = default) =>
        WriteAsync(
            async db =>
            {
                var root = new LibraryRootEntity
                {
                    Id = Guid.CreateVersion7(),
                    Path = LibraryPaths.NormalizeFull(path),
                    Kind = kind,
                    AddedAt = timeProvider.GetUtcNow(),
                };
                db.LibraryRoots.Add(root);

                var stats = SyncRoot(root, onLessonFound: null);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Pasta adicionada à biblioteca: {RootId} ({Kind}, {Courses} cursos)", root.Id, kind, root.Courses.Count);
                logger.LogDebug("Pasta {RootId}: {Path}; {Stats}", root.Id, root.Path, stats);
            },
            cancellationToken);

    public Task AddVideosAsync(IReadOnlyList<string> files, VideoDestination destination, CancellationToken cancellationToken = default) =>
        WriteAsync(
            async db =>
            {
                var course = destination switch
                {
                    VideoDestination.NewCourse newCourse => db.Courses.Add(NewFilesCourse(newCourse.Title, isLooseVideos: false)).Entity,
                    _ => await db.Courses.Include(c => c.Lessons).FirstOrDefaultAsync(c => c.IsLooseVideos, cancellationToken).ConfigureAwait(false)
                        ?? db.Courses.Add(NewFilesCourse(string.Empty, isLooseVideos: true)).Entity,
                };

                var now = timeProvider.GetUtcNow();
                foreach (var file in files.Select(LibraryPaths.NormalizeFull))
                {
                    var entry = fileSystem.GetFile(file);
                    course.Lessons.Add(new LessonEntity
                    {
                        Id = Guid.CreateVersion7(),
                        Path = file,
                        Title = Path.GetFileNameWithoutExtension(file),
                        FileSize = entry?.Size ?? 0,
                        LastWriteUtc = entry?.LastWriteUtc ?? default,
                        IsAvailable = entry is not null,
                        FirstSeenAt = now,
                    });
                }

                ReorderFileLessons(course);
                course.IsAvailable = true;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Vídeos adicionados: {Count} em {CourseId}", files.Count, course.Id);
            },
            cancellationToken);

    public Task RemoveCourseAsync(Guid courseId, CancellationToken cancellationToken = default) =>
        WriteAsync(
            async db =>
            {
                var course = await db.Courses.Include(c => c.Root).ThenInclude(r => r!.Courses)
                    .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken).ConfigureAwait(false);
                if (course is null)
                {
                    return;
                }

                if (course.Root is { } root)
                {
                    if (root.Courses.Count == 1)
                    {
                        // Último curso da pasta adicionada: a pasta sai da biblioteca junto.
                        db.LibraryRoots.Remove(root);
                    }
                    else
                    {
                        // Pasta de cursos: lembra a remoção para a próxima atualização não trazer de volta.
                        db.RemovedCourses.Add(new RemovedCourseEntity { RootId = root.Id, RelativePath = course.RelativePath ?? string.Empty });
                    }
                }

                db.Courses.Remove(course);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Curso removido da biblioteca: {CourseId}", courseId);
            },
            cancellationToken);

    public Task RemoveLessonAsync(Guid lessonId, CancellationToken cancellationToken = default) =>
        WriteAsync(
            async db =>
            {
                var lesson = await db.Lessons.FirstOrDefaultAsync(l => l.Id == lessonId, cancellationToken).ConfigureAwait(false);
                if (lesson is null)
                {
                    return;
                }

                var course = await db.Courses.Include(c => c.Lessons).FirstAsync(c => c.Id == lesson.CourseId, cancellationToken).ConfigureAwait(false);
                if (course.Kind != CourseKind.Files)
                {
                    throw new InvalidOperationException("Aulas de pasta só saem da biblioteca junto com o curso.");
                }

                course.Lessons.Remove(lesson);
                db.Lessons.Remove(lesson);
                if (course.Lessons.Count == 0)
                {
                    db.Courses.Remove(course);
                }
                else
                {
                    ReorderFileLessons(course);
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    public async Task<int> CountKnownLessonsAtAsync(Guid rootId, string newPath, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var root = await db.LibraryRoots.AsNoTracking().Include(r => r.Courses).ThenInclude(c => c.Lessons)
            .FirstAsync(r => r.Id == rootId, cancellationToken).ConfigureAwait(false);

        var scan = await Task.Run(() => _scanner.ScanRoot(LibraryPaths.NormalizeFull(newPath), root.Kind), cancellationToken).ConfigureAwait(false);
        var found = scan.Courses
            .SelectMany(c => c.Lessons.Select(l => Key(c.RelativePath, l.RelativePath)))
            .ToHashSet(LibraryPaths.Comparer);

        return root.Courses.SelectMany(c => c.Lessons.Select(l => Key(c.RelativePath ?? string.Empty, l.Path))).Count(found.Contains);
    }

    public Task RelocateRootAsync(Guid rootId, string newPath, CancellationToken cancellationToken = default) =>
        WriteAsync(
            async db =>
            {
                var root = await LoadRootAsync(db, rootId, cancellationToken).ConfigureAwait(false);
                var known = root.Courses.Sum(c => c.Lessons.Count);
                root.Path = LibraryPaths.NormalizeFull(newPath);

                var stats = SyncRoot(root, onLessonFound: null);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Pasta relocalizada: {RootId} ({Matched} de {Known} aulas encontradas)", rootId, stats.Matched, known);
            },
            cancellationToken);

    public Task RefreshAsync(IProgress<LibraryScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        // Uma atualização por vez: quem pede durante uma em andamento espera a mesma.
        lock (_refreshLock)
        {
            if (_currentRefresh is { IsCompleted: false } running)
            {
                return running;
            }

            _currentRefresh = Task.Run(() => RefreshCoreAsync(progress, cancellationToken), cancellationToken);
            return _currentRefresh;
        }
    }

    private async Task RefreshCoreAsync(IProgress<LibraryScanProgress>? progress, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var found = 0;
        var total = new SyncStats();

        void OnLessonFound()
        {
            found++;
            if (found % ProgressStep == 0)
            {
                progress?.Report(new LibraryScanProgress(found));
            }
        }

        List<Guid> rootIds;
        await using (var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
        {
            rootIds = await db.LibraryRoots.Select(r => r.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var rootId in rootIds)
        {
            await WriteAsync(
                async db =>
                {
                    var root = await LoadRootAsync(db, rootId, cancellationToken).ConfigureAwait(false);
                    total += SyncRoot(root, OnLessonFound);
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }

        await WriteAsync(
            async db =>
            {
                var fileCourses = await db.Courses.Include(c => c.Lessons).Where(c => c.Kind == CourseKind.Files)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                foreach (var course in fileCourses)
                {
                    total += SyncFileCourse(course, OnLessonFound);
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        progress?.Report(new LibraryScanProgress(found));
        logger.LogInformation(
            "Biblioteca atualizada: {Roots} pastas, {Lessons} aulas ({New} novas, {Missing} indisponíveis) em {Elapsed}",
            rootIds.Count,
            found,
            total.Added,
            total.Missing,
            stopwatch.Elapsed);
    }

    private SyncStats SyncRoot(LibraryRootEntity root, Action? onLessonFound)
    {
        var now = timeProvider.GetUtcNow();
        RootScan scan;
        try
        {
            scan = fileSystem.DirectoryExists(root.Path)
                ? _scanner.ScanRoot(root.Path, root.Kind, onLessonFound)
                : throw new DirectoryNotFoundException();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            logger.LogWarning("Pasta da biblioteca inacessível: {RootId}", root.Id);
            root.IsAvailable = false;
            foreach (var course in root.Courses)
            {
                MarkUnavailable(course);
            }

            return new SyncStats(Missing: root.Courses.Sum(c => c.Lessons.Count));
        }

        if (scan.SkippedDirectories > 0)
        {
            logger.LogWarning("Subpastas puladas por falta de permissão ou erro de leitura: {RootId} ({Count})", root.Id, scan.SkippedDirectories);
        }

        root.IsAvailable = true;
        root.LastScanAt = now;
        var removed = root.RemovedCourses.Select(r => r.RelativePath).ToHashSet(LibraryPaths.Comparer);
        var existing = root.Courses.ToDictionary(c => c.RelativePath ?? string.Empty, LibraryPaths.Comparer);
        var seen = new HashSet<string>(LibraryPaths.Comparer);
        var stats = new SyncStats();

        foreach (var (scanned, position) in scan.Courses.Where(c => !removed.Contains(c.RelativePath)).Select((c, i) => (c, i)))
        {
            seen.Add(scanned.RelativePath);
            if (!existing.TryGetValue(scanned.RelativePath, out var course))
            {
                course = new CourseEntity
                {
                    Id = Guid.CreateVersion7(),
                    Kind = CourseKind.Folder,
                    RelativePath = scanned.RelativePath,
                    Title = scanned.Title,
                    AddedAt = now,
                };
                root.Courses.Add(course);
            }

            course.Title = scanned.Title;
            course.Position = position;
            course.IsAvailable = true;
            stats += SyncFolderCourse(course, scanned, now);
        }

        foreach (var course in root.Courses.Where(c => !seen.Contains(c.RelativePath ?? string.Empty)))
        {
            stats += new SyncStats(Missing: course.Lessons.Count(l => l.IsAvailable));
            MarkUnavailable(course);
        }

        return stats;
    }

    private static SyncStats SyncFolderCourse(CourseEntity course, ScannedCourse scanned, DateTimeOffset now)
    {
        var modules = course.Modules.ToDictionary(m => m.RelativePath, LibraryPaths.Comparer);
        var moduleIds = new Dictionary<string, Guid>(LibraryPaths.Comparer);
        foreach (var module in course.Modules)
        {
            module.IsAvailable = false;
        }

        foreach (var scannedModule in scanned.Modules)
        {
            if (!modules.TryGetValue(scannedModule.RelativePath, out var module))
            {
                module = new ModuleEntity { Id = Guid.CreateVersion7(), RelativePath = scannedModule.RelativePath, Title = scannedModule.Title };
                course.Modules.Add(module);
            }

            module.Title = scannedModule.Title;
            module.Position = scannedModule.Position;
            module.IsAvailable = true;
            moduleIds[scannedModule.RelativePath] = module.Id;
        }

        var lessons = course.Lessons.ToDictionary(l => l.Path, LibraryPaths.Comparer);
        var seen = new HashSet<string>(LibraryPaths.Comparer);
        var stats = new SyncStats();

        foreach (var scannedLesson in scanned.Lessons)
        {
            seen.Add(scannedLesson.RelativePath);
            if (!lessons.TryGetValue(scannedLesson.RelativePath, out var lesson))
            {
                lesson = new LessonEntity { Id = Guid.CreateVersion7(), Path = scannedLesson.RelativePath, Title = scannedLesson.Title, FirstSeenAt = now };
                course.Lessons.Add(lesson);
                stats += new SyncStats(Added: 1);
            }
            else
            {
                stats += new SyncStats(Matched: 1);
            }

            lesson.Title = scannedLesson.Title;
            lesson.Position = scannedLesson.Position;
            lesson.FileSize = scannedLesson.Size;
            lesson.LastWriteUtc = scannedLesson.LastWriteUtc;
            lesson.ModuleId = scannedLesson.ModuleRelativePath is { } modulePath ? moduleIds[modulePath] : null;
            lesson.IsAvailable = true;
        }

        foreach (var lesson in course.Lessons.Where(l => !seen.Contains(l.Path)))
        {
            if (lesson.IsAvailable)
            {
                stats += new SyncStats(Missing: 1);
            }

            lesson.IsAvailable = false;
        }

        return stats;
    }

    private SyncStats SyncFileCourse(CourseEntity course, Action onLessonFound)
    {
        var stats = new SyncStats();
        foreach (var lesson in course.Lessons)
        {
            if (fileSystem.GetFile(lesson.Path) is { } entry)
            {
                lesson.FileSize = entry.Size;
                lesson.LastWriteUtc = entry.LastWriteUtc;
                lesson.IsAvailable = true;
                onLessonFound();
            }
            else
            {
                stats += new SyncStats(Missing: lesson.IsAvailable ? 1 : 0);
                lesson.IsAvailable = false;
            }
        }

        course.IsAvailable = course.Lessons.Any(l => l.IsAvailable);
        return stats;
    }

    private static void MarkUnavailable(CourseEntity course)
    {
        course.IsAvailable = false;
        course.Modules.ForEach(m => m.IsAvailable = false);
        course.Lessons.ForEach(l => l.IsAvailable = false);
    }

    private CourseEntity NewFilesCourse(string title, bool isLooseVideos) => new()
    {
        Id = Guid.CreateVersion7(),
        Kind = CourseKind.Files,
        IsLooseVideos = isLooseVideos,
        Title = title,
        AddedAt = timeProvider.GetUtcNow(),
    };

    private static void ReorderFileLessons(CourseEntity course)
    {
        var position = 0;
        foreach (var lesson in course.Lessons.OrderBy(l => l.Title, NaturalStringComparer.Instance))
        {
            lesson.Position = position++;
        }
    }

    private static Task<LibraryRootEntity> LoadRootAsync(VibePlayerDbContext db, Guid rootId, CancellationToken cancellationToken) =>
        db.LibraryRoots
            .Include(r => r.RemovedCourses)
            .Include(r => r.Courses).ThenInclude(c => c.Modules)
            .Include(r => r.Courses).ThenInclude(c => c.Lessons)
            .AsSplitQuery()
            .FirstAsync(r => r.Id == rootId, cancellationToken);

    private async Task WriteAsync(Func<VibePlayerDbContext, Task> operation, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await operation(db).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static string Key(string courseRelativePath, string lessonPath) => courseRelativePath + "\n" + lessonPath;

    private static CourseNode ToNode(CourseEntity course)
    {
        var lessons = course.Lessons.OrderBy(l => l.Position).ToList();
        var modules = course.Modules
            .Where(m => m.IsAvailable || lessons.Any(l => l.ModuleId == m.Id))
            .OrderBy(m => m.Position)
            .Select(m => new ModuleNode(m.Id, m.Title, m.IsAvailable, lessons.Where(l => l.ModuleId == m.Id).Select(ToNode).ToList()))
            .ToList();

        return new CourseNode(
            course.Id,
            course.Title,
            course.Kind,
            course.IsLooseVideos,
            course.IsAvailable,
            course.RootId,
            course.Root?.Path,
            course.Root?.IsAvailable ?? true,
            lessons.Where(l => l.ModuleId is null).Select(ToNode).ToList(),
            modules);
    }

    private static LessonNode ToNode(LessonEntity lesson) => new(lesson.Id, lesson.Title, lesson.IsAvailable);

    private readonly record struct SyncStats(int Added = 0, int Matched = 0, int Missing = 0)
    {
        public static SyncStats operator +(SyncStats a, SyncStats b) =>
            new(a.Added + b.Added, a.Matched + b.Matched, a.Missing + b.Missing);
    }
}
