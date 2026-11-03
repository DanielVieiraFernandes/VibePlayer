namespace VibePlayer.Core.Playback;

/// <summary>Lê os pixels de um quadro (BGRA, 4 bytes por pixel) enquanto ele está protegido.</summary>
public delegate void VideoFrameReader(ReadOnlySpan<byte> pixels, int width, int height, int stride);

/// <summary>
/// Quadros decodificados para o app desenhar (spec 0006, "Renderização do vídeo").
/// </summary>
/// <remarks>
/// O decodificador grava num buffer e troca com o que fica visível quando um quadro fica pronto;
/// <see cref="ReadLatestFrame"/> lê o visível sob lock, então o leitor deve só copiar e sair.
/// </remarks>
public interface IVideoFrameSource
{
    /// <summary>Um quadro novo está pronto (em qualquer thread; agrupe antes de redesenhar).</summary>
    event EventHandler? FrameReady;

    /// <returns>Se havia um quadro para ler.</returns>
    bool ReadLatestFrame(VideoFrameReader reader);
}
