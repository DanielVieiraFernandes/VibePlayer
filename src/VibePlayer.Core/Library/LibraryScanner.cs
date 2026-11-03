namespace VibePlayer.Core.Library;

public sealed record ScannedLesson(string RelativePath, string Title, string? ModuleRelativePath, long Size, DateTime LastWriteUtc, int Position);

public sealed record ScannedModule(string RelativePath, string Title, int Position);

/// <param name="RelativePath">Relativo à pasta adicionada; vazio quando ela própria é o curso.</param>
public sealed record ScannedCourse(string RelativePath, string Title, IReadOnlyList<ScannedModule> Modules, IReadOnlyList<ScannedLesson> Lessons);

/// <param name="IgnoredLooseVideos">Vídeos soltos numa pasta de cursos (não pertencem a nenhum curso).</param>
/// <param name="SkippedDirectories">Subpastas que não puderam ser lidas (permissão, erro de E/S).</param>
public sealed record RootScan(IReadOnlyList<ScannedCourse> Courses, int IgnoredLooseVideos, int SkippedDirectories);

/// <summary>
/// Lê uma pasta adicionada e devolve cursos → módulos → aulas, já filtrados e na ordem natural.
/// </summary>
/// <remarks>
/// Regras (spec 0005):
/// <list type="bullet">
/// <item>Vídeos direto na pasta do curso são aulas sem módulo e vêm primeiro.</item>
/// <item>Toda subpasta com vídeos, em qualquer profundidade, é um módulo; o aninhamento vira nome
/// composto ("Módulo 1 / Parte A"), porque o painel lateral não comporta árvore profunda.</item>
/// <item>Pastas sem vídeos não aparecem. Ocultos, arquivos de sistema e links ficam de fora; links
/// nunca são seguidos, o que também impede ciclos.</item>
/// <item>Uma subpasta ilegível é contada e pulada; só a pasta adicionada inacessível interrompe
/// (a exceção sobe para quem chamou marcar a pasta como indisponível).</item>
/// </list>
/// A posição de cada aula é única no curso (sem módulo primeiro, depois módulo a módulo), para a UI
/// só ler na ordem gravada: o SQLite não sabe ordenar "naturalmente".
/// </remarks>
public sealed class LibraryScanner(IFileSystemReader fileSystem)
{
    public const string ModuleTitleSeparator = " / ";

    // Profundidade máxima de módulos aninhados; cursos reais não passam de 3 ou 4.
    private const int MaxDepth = 32;

    private static readonly HashSet<string> SystemNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Thumbs.db", "desktop.ini", "@eaDir", "$RECYCLE.BIN", "System Volume Information", "__MACOSX",
    };

    /// <exception cref="UnauthorizedAccessException">A pasta adicionada não pode ser lida.</exception>
    /// <exception cref="IOException">A pasta adicionada está inacessível.</exception>
    public RootScan ScanRoot(string rootPath, LibraryRootKind kind, Action? onLessonFound = null)
    {
        var context = new ScanContext(onLessonFound);
        var entries = Visible(fileSystem.List(rootPath));

        if (kind == LibraryRootKind.Course)
        {
            var course = ScanCourse(rootPath, string.Empty, Path.GetFileName(rootPath), entries, context);
            return new RootScan(course.Lessons.Count > 0 ? [course] : [], 0, context.SkippedDirectories);
        }

        var courses = new List<ScannedCourse>();
        foreach (var directory in Directories(entries))
        {
            if (TryList(directory.FullPath, context) is { } courseEntries)
            {
                var course = ScanCourse(directory.FullPath, LibraryPaths.Relative(rootPath, directory.FullPath), directory.Name, courseEntries, context);
                if (course.Lessons.Count > 0)
                {
                    courses.Add(course);
                }
            }
        }

        return new RootScan(courses, Videos(entries).Count(), context.SkippedDirectories);
    }

    /// <summary>As duas interpretações possíveis da pasta e qual o app sugere.</summary>
    public FolderPreview Preview(string path)
    {
        var asCourse = ScanRoot(path, LibraryRootKind.Course);
        var asCollection = ScanRoot(path, LibraryRootKind.Collection);
        return new FolderPreview(path, Suggest(path), ToPreview(asCourse), ToPreview(asCollection));
    }

    /// <summary>
    /// "Vários cursos" quando alguma subpasta direta tem subpastas com vídeos (formato
    /// Pasta/Curso/Módulo/aula). Só decide a opção pré-selecionada: o usuário sempre confirma.
    /// </summary>
    public LibraryRootKind Suggest(string path)
    {
        var context = new ScanContext(null);
        foreach (var directory in Directories(Visible(fileSystem.List(path))))
        {
            var children = TryList(directory.FullPath, context) ?? [];
            if (Directories(children).Any(child => ContainsVideos(child.FullPath, context, depth: 1)))
            {
                return LibraryRootKind.Collection;
            }
        }

        return LibraryRootKind.Course;
    }

    private ScannedCourse ScanCourse(string coursePath, string relativePath, string title, IReadOnlyList<FileSystemEntry> entries, ScanContext context)
    {
        var modules = new List<ScannedModule>();
        var lessons = new List<ScannedLesson>();

        AddLessons(coursePath, moduleRelativePath: null, Videos(entries), lessons, context);
        ScanModules(coursePath, coursePath, titlePrefix: null, entries, modules, lessons, context, depth: 1);

        return new ScannedCourse(relativePath, title, modules, lessons);
    }

    private void ScanModules(
        string coursePath,
        string directoryPath,
        string? titlePrefix,
        IReadOnlyList<FileSystemEntry> entries,
        List<ScannedModule> modules,
        List<ScannedLesson> lessons,
        ScanContext context,
        int depth)
    {
        if (depth > MaxDepth)
        {
            return;
        }

        foreach (var directory in Directories(entries))
        {
            if (TryList(directory.FullPath, context) is not { } children)
            {
                continue;
            }

            var title = titlePrefix is null ? directory.Name : titlePrefix + ModuleTitleSeparator + directory.Name;
            var videos = Videos(children).ToList();
            if (videos.Count > 0)
            {
                var moduleRelativePath = LibraryPaths.Relative(coursePath, directory.FullPath);
                modules.Add(new ScannedModule(moduleRelativePath, title, modules.Count));
                AddLessons(coursePath, moduleRelativePath, videos, lessons, context);
            }

            ScanModules(coursePath, directory.FullPath, title, children, modules, lessons, context, depth + 1);
        }
    }

    private static void AddLessons(string coursePath, string? moduleRelativePath, IEnumerable<FileSystemEntry> videos, List<ScannedLesson> lessons, ScanContext context)
    {
        foreach (var video in videos)
        {
            lessons.Add(new ScannedLesson(
                LibraryPaths.Relative(coursePath, video.FullPath),
                Path.GetFileNameWithoutExtension(video.Name),
                moduleRelativePath,
                video.Size,
                video.LastWriteUtc,
                lessons.Count));
            context.OnLessonFound?.Invoke();
        }
    }

    private bool ContainsVideos(string path, ScanContext context, int depth)
    {
        if (depth > MaxDepth || TryList(path, context) is not { } entries)
        {
            return false;
        }

        return Videos(entries).Any() || Directories(entries).Any(d => ContainsVideos(d.FullPath, context, depth + 1));
    }

    private IReadOnlyList<FileSystemEntry>? TryList(string path, ScanContext context)
    {
        try
        {
            return Visible(fileSystem.List(path));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            context.SkippedDirectories++;
            return null;
        }
    }

    private static List<FileSystemEntry> Visible(IReadOnlyList<FileSystemEntry> entries) =>
        entries.Where(e => !e.IsHidden && !e.IsLink && !e.Name.StartsWith('.') && !SystemNames.Contains(e.Name)).ToList();

    private static IEnumerable<FileSystemEntry> Directories(IEnumerable<FileSystemEntry> entries) =>
        entries.Where(e => e.IsDirectory).OrderBy(e => e.Name, NaturalStringComparer.Instance);

    private static IEnumerable<FileSystemEntry> Videos(IEnumerable<FileSystemEntry> entries) =>
        entries.Where(e => !e.IsDirectory && VideoFormats.IsVideo(e.Name)).OrderBy(e => e.Name, NaturalStringComparer.Instance);

    private static InterpretationPreview ToPreview(RootScan scan) =>
        new(scan.Courses.Select(c => new PreviewCourse(c.Title, c.Modules.Count, c.Lessons.Count)).ToList(), scan.IgnoredLooseVideos);

    private sealed class ScanContext(Action? onLessonFound)
    {
        public Action? OnLessonFound { get; } = onLessonFound;

        public int SkippedDirectories { get; set; }
    }
}
