namespace VibePlayer.Core.Playback;

/// <summary>Preenche a duração das aulas da biblioteca, em segundo plano.</summary>
public interface ILessonDurationUpdater
{
    /// <param name="batchSaved">Chamado a cada lote gravado (para a árvore recarregar), em qualquer thread.</param>
    Task UpdateMissingAsync(Action? batchSaved = null, CancellationToken cancellationToken = default);
}
