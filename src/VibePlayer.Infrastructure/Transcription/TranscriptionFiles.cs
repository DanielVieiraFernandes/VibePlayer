using System.Security.Cryptography;
using VibePlayer.Core.Library;
using VibePlayer.Core.Transcription;

namespace VibePlayer.Infrastructure.Transcription;

/// <summary>SHA-256 do arquivo inteiro. Compartilha o arquivo porque o player pode estar lendo o mesmo vídeo.</summary>
public sealed class Sha256FileHasher : IFileHasher
{
    private const int BufferSize = 1 << 20;

    public async Task<string> ComputeAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                BufferSize = BufferSize,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }
}

/// <summary>Procura a legenda na pasta do vídeo, sem nunca escrever nela.</summary>
public sealed class PhysicalSubtitleSource(IFileSystemReader fileSystem) : ISubtitleSource
{
    private static readonly string[] Portuguese = ["pt", "pt-br", "por"];

    public string? Find(string videoPath)
    {
        ArgumentNullException.ThrowIfNull(videoPath);
        var directory = Path.GetDirectoryName(videoPath);
        if (directory is null || !fileSystem.DirectoryExists(directory))
        {
            return null;
        }

        var videoName = Path.GetFileNameWithoutExtension(videoPath);
        return fileSystem.List(directory)
            .Where(entry => !entry.IsDirectory)
            .Select(entry => (entry.FullPath, Rank: Rank(videoName, entry.Name)))
            .Where(candidate => candidate.Rank >= 0)
            .OrderBy(candidate => candidate.Rank)
            .ThenBy(candidate => candidate.FullPath, StringComparer.Ordinal)
            .Select(candidate => candidate.FullPath)
            .FirstOrDefault();
    }

    public Task<byte[]> ReadAsync(string subtitlePath, CancellationToken cancellationToken = default) =>
        File.ReadAllBytesAsync(subtitlePath, cancellationToken);

    /// <summary>Menor é melhor: português, depois sem idioma, depois outros; SRT antes de VTT. -1 não é legenda desta aula.</summary>
    private static int Rank(string videoName, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToUpperInvariant();
        if (extension is not (".SRT" or ".VTT"))
        {
            return -1;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var format = extension == ".SRT" ? 0 : 1;
        if (string.Equals(stem, videoName, StringComparison.OrdinalIgnoreCase))
        {
            return 2 + format;
        }

        if (!stem.StartsWith(videoName + ".", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        var language = stem[(videoName.Length + 1)..];
        return language.Length is < 2 or > 5 || language.Contains('.', StringComparison.Ordinal)
            ? -1
            : Portuguese.Contains(language, StringComparer.OrdinalIgnoreCase) ? format : 4 + format;
    }
}
