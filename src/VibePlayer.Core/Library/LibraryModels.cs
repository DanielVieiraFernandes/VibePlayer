namespace VibePlayer.Core.Library;

/// <summary>Como uma pasta adicionada é interpretada.</summary>
public enum LibraryRootKind
{
    /// <summary>A pasta é um curso; subpastas são módulos.</summary>
    Course,

    /// <summary>A pasta contém cursos; cada subpasta direta com vídeos é um curso.</summary>
    Collection,
}

public enum CourseKind
{
    /// <summary>Curso que vem de uma pasta adicionada.</summary>
    Folder,

    /// <summary>Curso montado com vídeos avulsos (caminhos absolutos, sem módulos).</summary>
    Files,
}

// Modelos de leitura para a UI: imutáveis e já na ordem de exibição.

public enum LessonStatus
{
    NotStarted,
    InProgress,
    Completed,
}

/// <param name="Duration">Nula enquanto o arquivo não foi lido.</param>
/// <param name="Thumbnail">O que identifica a miniatura desta versão do arquivo.</param>
public sealed record LessonNode(
    Guid Id,
    string Title,
    bool IsAvailable,
    TimeSpan? Duration = null,
    LessonStatus Status = LessonStatus.NotStarted,
    Playback.ThumbnailRequest? Thumbnail = null);

public sealed record ModuleNode(Guid Id, string Title, bool IsAvailable, IReadOnlyList<LessonNode> Lessons);

/// <param name="RootId">Pasta adicionada de onde o curso veio; nulo em cursos de vídeos avulsos.</param>
/// <param name="RootPath">Caminho da pasta adicionada, para mostrar onde ela era esperada.</param>
/// <param name="IsRootAvailable">Falso quando a pasta adicionada inteira está inacessível.</param>
public sealed record CourseNode(
    Guid Id,
    string Title,
    CourseKind Kind,
    bool IsLooseVideos,
    bool IsAvailable,
    Guid? RootId,
    string? RootPath,
    bool IsRootAvailable,
    IReadOnlyList<LessonNode> Lessons,
    IReadOnlyList<ModuleNode> Modules)
{
    public int LessonCount => Lessons.Count + Modules.Sum(m => m.Lessons.Count);

    public IEnumerable<LessonNode> AllLessons => Lessons.Concat(Modules.SelectMany(m => m.Lessons));

    public int CompletedCount => AllLessons.Count(l => l.Status == LessonStatus.Completed);

    /// <summary>Soma das aulas com duração conhecida.</summary>
    public TimeSpan TotalDuration => AllLessons.Aggregate(TimeSpan.Zero, (sum, l) => sum + (l.Duration ?? TimeSpan.Zero));
}

// Prévia antes de adicionar.

public sealed record PreviewCourse(string Title, int ModuleCount, int LessonCount);

/// <param name="IgnoredLooseVideos">Vídeos soltos na pasta de cursos, que não pertencem a nenhum curso.</param>
public sealed record InterpretationPreview(IReadOnlyList<PreviewCourse> Courses, int IgnoredLooseVideos);

public sealed record FolderPreview(
    string Path,
    LibraryRootKind Suggested,
    InterpretationPreview AsCourse,
    InterpretationPreview AsCollection);

/// <summary>Para onde vão vídeos adicionados avulsos.</summary>
public abstract record VideoDestination
{
    public sealed record NewCourse(string Title) : VideoDestination;

    public sealed record LooseVideos : VideoDestination;
}

/// <summary>Progresso de uma atualização da biblioteca.</summary>
public sealed record LibraryScanProgress(int LessonsFound);

/// <summary>O que já está na biblioteca, para validar o que o usuário quer adicionar.</summary>
public sealed record LibraryKnownPaths(IReadOnlyList<string> RootPaths, IReadOnlyList<string> VideoFiles);
