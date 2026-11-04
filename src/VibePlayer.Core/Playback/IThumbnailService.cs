namespace VibePlayer.Core.Playback;

/// <summary>Qual versão de qual vídeo uma miniatura representa.</summary>
/// <param name="FileSize">Junto com <paramref name="LastWriteUtc"/>, identifica a versão do arquivo: mudou, a miniatura é refeita.</param>
public sealed record ThumbnailRequest(Guid LessonId, string FilePath, long FileSize, DateTime LastWriteUtc, TimeSpan? Duration);

public enum ThumbnailPriority
{
    /// <summary>Capa de curso.</summary>
    Low,

    /// <summary>Aulas de um curso aberto na árvore.</summary>
    Normal,

    /// <summary>A aula selecionada.</summary>
    High,
}

/// <summary>Miniaturas das aulas: cache em disco e geração em segundo plano, uma por vez.</summary>
public interface IThumbnailService
{
    /// <summary>Caminho da miniatura já gerada para esta versão do arquivo, ou <c>null</c>.</summary>
    string? TryGetPath(ThumbnailRequest request);

    /// <summary>Pede a geração (sem efeito se já existe, já está na fila ou já falhou para esta versão).</summary>
    void Request(ThumbnailRequest request, ThumbnailPriority priority);

    /// <summary>Uma miniatura ficou pronta (em qualquer thread).</summary>
    event EventHandler<Guid>? ThumbnailReady;
}
