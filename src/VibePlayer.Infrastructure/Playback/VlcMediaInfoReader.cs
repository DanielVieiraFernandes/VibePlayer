using LibVLCSharp.Shared;
using VibePlayer.Core.Playback;

namespace VibePlayer.Infrastructure.Playback;

/// <summary>Duração dos vídeos pelo próprio VLC, lendo só os metadados (sem decodificar).</summary>
public sealed class VlcMediaInfoReader(VlcRuntime runtime) : IMediaInfoReader
{
    private const int ParseTimeoutMs = 5000;

    public async Task<TimeSpan?> ReadDurationAsync(string path, CancellationToken cancellationToken = default)
    {
        if (runtime.LibVlc is not { } libVlc || !File.Exists(path))
        {
            return null;
        }

        using var media = new Media(libVlc, new Uri(path));
        var status = await media.Parse(MediaParseOptions.ParseLocal, ParseTimeoutMs, cancellationToken).ConfigureAwait(false);

        // O Parse do LibVLCSharp termina dentro do evento do VLC, e o código depois do await
        // continuaria na thread dele; liberar a mídia (ou abrir outra) ali é chamada reentrante e
        // derruba o processo. Yield sai para o pool de threads antes de qualquer outra coisa.
        await Task.Yield();
        return status == MediaParsedStatus.Done && media.Duration > 0 ? TimeSpan.FromMilliseconds(media.Duration) : null;
    }
}
