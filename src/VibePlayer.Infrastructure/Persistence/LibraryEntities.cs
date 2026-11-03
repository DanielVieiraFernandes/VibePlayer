using VibePlayer.Core.Library;

namespace VibePlayer.Infrastructure.Persistence;

// Entidades da biblioteca (spec 0005). Os Ids são estáveis: progresso, transcrição e chat das
// próximas specs apontam para LessonEntity.Id, e uma aula que some e volta mantém o mesmo Id.

public sealed class LibraryRootEntity
{
    public Guid Id { get; set; }

    public required string Path { get; set; }

    public LibraryRootKind Kind { get; set; }

    public DateTimeOffset AddedAt { get; set; }

    public DateTimeOffset? LastScanAt { get; set; }

    public bool IsAvailable { get; set; } = true;

    public List<CourseEntity> Courses { get; set; } = [];

    /// <summary>Cursos de uma pasta de cursos que o usuário removeu; a atualização não os traz de volta.</summary>
    public List<RemovedCourseEntity> RemovedCourses { get; set; } = [];
}

public sealed class RemovedCourseEntity
{
    public Guid RootId { get; set; }

    public required string RelativePath { get; set; }
}

public sealed class CourseEntity
{
    public Guid Id { get; set; }

    public Guid? RootId { get; set; }

    public LibraryRootEntity? Root { get; set; }

    public CourseKind Kind { get; set; }

    /// <summary>O curso fixo "Vídeos avulsos" (título exibido vem dos textos da UI).</summary>
    public bool IsLooseVideos { get; set; }

    /// <summary>Relativo à raiz ("/" como separador); vazio quando a raiz é o curso; nulo em cursos de arquivos.</summary>
    public string? RelativePath { get; set; }

    public required string Title { get; set; }

    public int Position { get; set; }

    public bool IsAvailable { get; set; } = true;

    public DateTimeOffset AddedAt { get; set; }

    public List<ModuleEntity> Modules { get; set; } = [];

    public List<LessonEntity> Lessons { get; set; } = [];
}

public sealed class ModuleEntity
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public required string RelativePath { get; set; }

    public required string Title { get; set; }

    public int Position { get; set; }

    public bool IsAvailable { get; set; } = true;
}

public sealed class LessonEntity
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public Guid? ModuleId { get; set; }

    /// <summary>Relativo ao curso em cursos de pasta ("/" como separador); absoluto em cursos de arquivos.</summary>
    public required string Path { get; set; }

    public required string Title { get; set; }

    public int Position { get; set; }

    public long FileSize { get; set; }

    public DateTime LastWriteUtc { get; set; }

    public bool IsAvailable { get; set; } = true;

    public DateTimeOffset FirstSeenAt { get; set; }
}
