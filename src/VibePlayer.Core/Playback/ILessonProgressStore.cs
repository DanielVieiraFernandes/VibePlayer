namespace VibePlayer.Core.Playback;

/// <summary>Progresso salvo de uma aula.</summary>
public sealed record LessonProgress(Guid LessonId, TimeSpan Position, bool IsCompleted, DateTimeOffset LastWatchedAt);

/// <summary>O que é preciso para tocar uma aula: onde está o arquivo e o que já se sabe dela.</summary>
public sealed record PlayableLesson(Guid Id, Guid CourseId, string Title, string CourseTitle, string FilePath, TimeSpan? Duration);

/// <summary>Progresso das aulas (posição, concluída) e a última aula assistida.</summary>
public interface ILessonProgressStore
{
    Task<LessonProgress?> GetAsync(Guid lessonId, CancellationToken cancellationToken = default);

    Task SavePositionAsync(Guid lessonId, TimeSpan position, CancellationToken cancellationToken = default);

    Task SetCompletedAsync(Guid lessonId, bool completed, CancellationToken cancellationToken = default);

    /// <summary>Caminho do arquivo, títulos e duração; <c>null</c> se a aula não existe ou está indisponível.</summary>
    Task<PlayableLesson?> GetPlayableAsync(Guid lessonId, CancellationToken cancellationToken = default);

    /// <summary>Aulas do curso na ordem de exibição, com disponibilidade (para achar a próxima e a anterior).</summary>
    Task<IReadOnlyList<(Guid Id, bool IsAvailable)>> GetCourseOrderAsync(Guid courseId, CancellationToken cancellationToken = default);

    Task SaveDurationAsync(Guid lessonId, TimeSpan duration, CancellationToken cancellationToken = default);
}
