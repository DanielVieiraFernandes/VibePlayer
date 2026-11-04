using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using VibePlayer.Core;
using VibePlayer.Core.Transcription;

namespace VibePlayer.Infrastructure.Transcription;

/// <summary>
/// Baixa os modelos do Whisper. É a única conexão de rede da transcrição, e não leva dado do usuário.
/// </summary>
/// <remarks>
/// O download do próprio Whisper.net não serve: não retoma, e modelos passam de 500 MB em conexões
/// domésticas. Baixa para <c>.part</c> e só renomeia depois de conferir o SHA-256, por isso um
/// arquivo no nome final está sempre inteiro. O commit na URL é fixo, para o hash não mudar.
/// </remarks>
public sealed partial class WhisperModelStore(
    IAppPaths paths,
    HttpClient httpClient,
    Func<string, long> freeSpaceOf,
    ILogger<WhisperModelStore> logger) : IWhisperModelStore
{
    public const string BaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/";

    private const int BufferSize = 1 << 20;

    // Sem dado novo por este tempo, a conexão morreu: HttpClient.Timeout não ajuda num download
    // longo, porque conta o tempo total.
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);

    public bool IsAvailable(WhisperModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var file = new FileInfo(PathOf(model));
        return file.Exists && file.Length == model.SizeBytes;
    }

    public string PathOf(WhisperModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return Path.Combine(paths.ModelsDirectory, model.FileName);
    }

    public async Task DownloadAsync(WhisperModel model, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        Directory.CreateDirectory(paths.ModelsDirectory);
        var partial = PathOf(model) + ".part";
        try
        {
            if (new FileInfo(partial) is { Exists: true } existing && existing.Length > model.SizeBytes)
            {
                existing.Delete();
            }

            var downloaded = File.Exists(partial) ? new FileInfo(partial).Length : 0;
            if (freeSpaceOf(paths.ModelsDirectory) < model.SizeBytes - downloaded)
            {
                throw new ModelDownloadException(ModelDownloadError.NotEnoughSpace);
            }

            if (downloaded < model.SizeBytes)
            {
                await FetchAsync(model, partial, downloaded, progress, cancellationToken).ConfigureAwait(false);
            }

            await VerifyAsync(model, partial, cancellationToken).ConfigureAwait(false);
            File.Move(partial, PathOf(model), overwrite: true);
            LogDownloaded(model.Name, model.SizeBytes);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            throw new ModelDownloadException(ModelDownloadError.Network, ex);
        }
    }

    private async Task FetchAsync(WhisperModel model, string partial, long downloaded, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + model.FileName);
        if (downloaded > 0)
        {
            request.Headers.Range = new RangeHeaderValue(downloaded, null);
        }

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // Servidor que ignora o Range devolve 200 com o arquivo inteiro: começa do zero.
        var resumed = response.StatusCode == HttpStatusCode.PartialContent;
        var position = resumed ? downloaded : 0;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = new FileStream(partial, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

        var buffer = new byte[BufferSize];
        while (true)
        {
            using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stall.CancelAfter(StallTimeout);
            int read;
            try
            {
                read = await source.ReadAsync(buffer, stall.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ModelDownloadException(ModelDownloadError.Network, ex);
            }

            if (read == 0)
            {
                return;
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            position += read;
            progress?.Report(Math.Min((double)position / model.SizeBytes, 1));
        }
    }

    private static async Task VerifyAsync(WhisperModel model, string partial, CancellationToken cancellationToken)
    {
        string hash;
        await using (var stream = new FileStream(partial, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true))
        {
            hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        }

        if (!string.Equals(hash, model.Sha256, StringComparison.Ordinal))
        {
            File.Delete(partial);
            throw new ModelDownloadException(ModelDownloadError.HashMismatch);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Modelo {Model} baixado ({Size} bytes)")]
    private partial void LogDownloaded(string model, long size);
}
