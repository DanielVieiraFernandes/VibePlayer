namespace VibePlayer.Core.Playback;

/// <summary>Lê metadados de vídeos sem tocá-los.</summary>
public interface IMediaInfoReader
{
    /// <returns>A duração, ou <c>null</c> se o arquivo não pôde ser lido.</returns>
    Task<TimeSpan?> ReadDurationAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>Tira um quadro de um vídeo e grava como imagem.</summary>
public interface IThumbnailGenerator
{
    /// <returns>Se a imagem foi gravada em <paramref name="outputPath"/>.</returns>
    Task<bool> GenerateAsync(string videoPath, TimeSpan at, string outputPath, CancellationToken cancellationToken = default);
}
