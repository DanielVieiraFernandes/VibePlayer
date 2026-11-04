namespace VibePlayer.Core.Transcription;

/// <summary>
/// Transcrição das aulas: legendas ao lado do vídeo, cache por hash e fila do Whisper, uma aula
/// por vez, em segundo plano.
/// </summary>
/// <remarks>
/// Os eventos chegam em threads de segundo plano; quem atualiza a interface passa pelo dispatcher.
/// </remarks>
public interface ITranscriptionService
{
    /// <summary>A situação de uma aula mudou.</summary>
    event EventHandler<Guid>? Changed;

    /// <summary>Andamento da aula em transcrição. Bem mais frequente que <see cref="Changed"/>.</summary>
    event EventHandler<TranscriptionProgress>? ProgressChanged;

    /// <summary>
    /// Situação da aula. Procura legenda ao lado do vídeo e descarta a transcrição de um arquivo
    /// que mudou, por isso pode ler o disco.
    /// </summary>
    Task<LessonTranscription> GetAsync(Guid lessonId, CancellationToken cancellationToken = default);

    /// <summary>Põe aulas na fila. Quem já está pronta ou na fila é ignorada.</summary>
    Task EnqueueAsync(IReadOnlyList<Guid> lessonIds, CancellationToken cancellationToken = default);

    /// <summary>"Transcrever com o Whisper": troca a legenda pelo Whisper nesta aula.</summary>
    Task EnqueueWithWhisperAsync(Guid lessonId, CancellationToken cancellationToken = default);

    /// <summary>Tira a aula da fila, ou encerra a transcrição em andamento.</summary>
    Task CancelAsync(Guid lessonId, CancellationToken cancellationToken = default);

    /// <summary>Marca a aula aberta no player, que passa à frente na fila.</summary>
    void SetCurrentLesson(Guid? lessonId);

    /// <summary>Retoma a fila salva. Chamado uma vez, ao abrir o app.</summary>
    Task ResumeAsync(CancellationToken cancellationToken = default);
}
