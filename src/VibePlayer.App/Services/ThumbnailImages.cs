using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;

namespace VibePlayer.App.Services;

/// <summary>
/// Miniaturas já decodificadas para a UI, com cache em memória das mais recentes.
/// </summary>
/// <remarks>
/// A árvore da biblioteca é recriada a cada recarga (ver LibraryItemViewModel); sem este cache,
/// cada recarga releria do disco todas as miniaturas visíveis. A decodificação roda fora da thread
/// de UI e já reduz para a largura usada na tela (2× para telas de alta densidade).
/// </remarks>
public sealed class ThumbnailImages(ILogger<ThumbnailImages> logger)
{
    public const int DecodeWidth = 128;
    public const int PosterDecodeWidth = 640;
    private const int Capacity = 300;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, LinkedListNode<(string Key, Bitmap Image)>> _index = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Key, Bitmap Image)> _recent = new();

    public async Task<Bitmap?> LoadAsync(string path, int width = DecodeWidth)
    {
        var key = path + "|" + width;
        lock (_lock)
        {
            if (_index.TryGetValue(key, out var node))
            {
                _recent.Remove(node);
                _recent.AddFirst(node);
                return node.Value.Image;
            }
        }

        Bitmap image;
        try
        {
            image = await Task.Run(() =>
            {
                using var stream = File.OpenRead(path);
                return Bitmap.DecodeToWidth(stream, width, BitmapInterpolationMode.HighQuality);
            }).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            logger.LogDebug(ex, "Miniatura ilegível: {Path}", path);
            return null;
        }

        lock (_lock)
        {
            if (!_index.ContainsKey(key))
            {
                _index[key] = _recent.AddFirst((key, image));
                while (_recent.Count > Capacity)
                {
                    // Sai da lista, mas não é descartada: um item da tela pode ainda estar usando.
                    _index.Remove(_recent.Last!.Value.Key);
                    _recent.RemoveLast();
                }
            }
        }

        return image;
    }
}
