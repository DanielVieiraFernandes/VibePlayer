namespace VibePlayer.Core.Library;

/// <summary>
/// Biblioteca de cursos: o que o usuário adicionou e a árvore de cursos → módulos → aulas.
/// </summary>
/// <remarks>
/// Toda operação que lê pastas roda fora da thread de UI. Operações que gravam são serializadas
/// (uma por vez); <see cref="RefreshAsync"/> pedido durante outra atualização espera a que está
/// rodando em vez de começar uma segunda.
/// Validação (pasta existe, sem sobreposição, vídeos repetidos) é de quem chama, com
/// <see cref="LibraryFolderValidator"/> e <see cref="VideoSelectionValidator"/>.
/// </remarks>
public interface ICourseLibrary
{
    Task<IReadOnlyList<CourseNode>> GetCoursesAsync(CancellationToken cancellationToken = default);

    Task<LibraryKnownPaths> GetKnownPathsAsync(CancellationToken cancellationToken = default);

    /// <summary>As duas interpretações da pasta, sem gravar nada.</summary>
    Task<FolderPreview> PreviewFolderAsync(string path, CancellationToken cancellationToken = default);

    Task AddFolderAsync(string path, LibraryRootKind kind, CancellationToken cancellationToken = default);

    Task AddVideosAsync(IReadOnlyList<string> files, VideoDestination destination, CancellationToken cancellationToken = default);

    /// <summary>Remove o curso e seus registros; nunca toca nos arquivos do usuário.</summary>
    Task RemoveCourseAsync(Guid courseId, CancellationToken cancellationToken = default);

    /// <summary>Remove uma aula de um curso de vídeos avulsos (aulas de pasta só saem com o curso).</summary>
    Task RemoveLessonAsync(Guid lessonId, CancellationToken cancellationToken = default);

    /// <summary>Quantas aulas já conhecidas da pasta adicionada existem em <paramref name="newPath"/>.</summary>
    Task<int> CountKnownLessonsAtAsync(Guid rootId, string newPath, CancellationToken cancellationToken = default);

    /// <summary>"Localizar pasta": aponta a pasta adicionada para o novo lugar e reconecta as aulas.</summary>
    Task RelocateRootAsync(Guid rootId, string newPath, CancellationToken cancellationToken = default);

    /// <summary>Relê todas as pastas e vídeos e sincroniza com o banco.</summary>
    Task RefreshAsync(IProgress<LibraryScanProgress>? progress = null, CancellationToken cancellationToken = default);
}
