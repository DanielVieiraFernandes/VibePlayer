using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VibePlayer.Core;
using VibePlayer.Core.Playback;
using VibePlayer.Core.Threading;

namespace VibePlayer.Infrastructure.Playback;

/// <summary>
/// Cache de miniaturas em <c>&lt;dados&gt;/thumbnails</c> e fila de geração, uma por vez.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Nome do arquivo = aula + tamanho + data do vídeo. Vídeo alterado procura outro nome, e
/// a miniatura é refeita sem precisar de estado no banco; as antigas daquela aula são apagadas
/// quando a nova fica pronta.</item>
/// <item>Fila com prioridade (selecionada, curso aberto, capa) e sem repetição. Uma geração por vez:
/// cada uma abre um decodificador, e várias juntas disputariam a CPU com o vídeo tocando.</item>
/// <item>Vídeo que não deu miniatura ganha um marcador <c>.failed</c> para aquela versão do arquivo:
/// a próxima abertura não tenta de novo (e não gasta segundos esperando o mesmo erro).</item>
/// </list>
/// </remarks>
public sealed class ThumbnailService(
    IThumbnailGenerator generator,
    IAppPaths paths,
    ILogger<ThumbnailService> logger) : IThumbnailService, IDisposable
{
    private const string Extension = ".jpg";
    private const string FailedExtension = ".failed";

    // Sem duração conhecida, 5 s costuma passar da vinheta e cair no conteúdo.
    private static readonly TimeSpan PositionWithoutDuration = TimeSpan.FromSeconds(5);

    private readonly Lock _lock = new();
    private readonly PriorityQueue<ThumbnailRequest, (int Priority, long Order)> _queue = new();
    private readonly HashSet<string> _pending = [];
    private readonly CancellationTokenSource _shutdown = new();
    private long _order;
    private bool _working;

    public event EventHandler<Guid>? ThumbnailReady;

    public string? TryGetPath(ThumbnailRequest request)
    {
        var path = PathFor(request);
        return File.Exists(path) ? path : null;
    }

    public void Request(ThumbnailRequest request, ThumbnailPriority priority)
    {
        var key = Key(request);
        if (File.Exists(PathFor(request)) || File.Exists(FailedPathFor(request)))
        {
            return;
        }

        lock (_lock)
        {
            // Já na fila: entra de novo só se a prioridade subiu (a cópia antiga é ignorada ao sair).
            if (!_pending.Add(key) && priority == ThumbnailPriority.Low)
            {
                return;
            }

            _queue.Enqueue(request, (-(int)priority, _order++));
            if (_working)
            {
                return;
            }

            _working = true;
        }

        ProcessQueueAsync().FireAndForget(logger, "GerarMiniaturas");
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
    }

    private async Task ProcessQueueAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var generated = 0;
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                ThumbnailRequest request;
                lock (_lock)
                {
                    if (!_queue.TryDequeue(out request!, out _))
                    {
                        _working = false;
                        break;
                    }

                    if (!_pending.Remove(Key(request)))
                    {
                        continue;
                    }
                }

                if (await GenerateAsync(request).ConfigureAwait(false))
                {
                    generated++;
                    ThumbnailReady?.Invoke(this, request.LessonId);
                }
            }
        }
        finally
        {
            lock (_lock)
            {
                _working = false;
            }

            if (generated > 0)
            {
                logger.LogInformation("Miniaturas geradas: {Count} em {Elapsed}", generated, stopwatch.Elapsed);
            }
        }
    }

    private async Task<bool> GenerateAsync(ThumbnailRequest request)
    {
        var output = PathFor(request);
        var at = request.Duration is { } duration && duration > TimeSpan.Zero
            ? duration * 0.1
            : PositionWithoutDuration;

        bool ok;
        try
        {
            // Task.Run: abrir o decodificador do VLC é síncrono e não pode ocupar a thread de UI.
            ok = await Task.Run(() => generator.GenerateAsync(request.FilePath, at, output, _shutdown.Token), _shutdown.Token).ConfigureAwait(false);

            // Sem duração conhecida, os 5 s podem passar do fim de um vídeo curto; aí tenta o começo.
            if (!ok && at > TimeSpan.Zero)
            {
                ok = await Task.Run(() => generator.GenerateAsync(request.FilePath, TimeSpan.Zero, output, _shutdown.Token), _shutdown.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Miniatura não gerada para {LessonId}", request.LessonId);
            ok = false;
        }

        if (!ok)
        {
            logger.LogDebug("Miniatura não gerada para {LessonId}; não será tentada de novo para esta versão do arquivo", request.LessonId);
            await File.WriteAllBytesAsync(FailedPathFor(request), []).ConfigureAwait(false);
            return false;
        }

        DeleteOlderVersions(request);
        return true;
    }

    private void DeleteOlderVersions(ThumbnailRequest request)
    {
        var current = Key(request);
        foreach (var file in Directory.EnumerateFiles(paths.ThumbnailsDirectory, request.LessonId.ToString("N") + "-*"))
        {
            if (!Path.GetFileNameWithoutExtension(file).Equals(current, StringComparison.Ordinal))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // Em uso (a UI pode estar mostrando a antiga); sai na próxima vez.
                }
            }
        }
    }

    private string PathFor(ThumbnailRequest request) => Path.Combine(paths.ThumbnailsDirectory, Key(request) + Extension);

    private string FailedPathFor(ThumbnailRequest request) => Path.Combine(paths.ThumbnailsDirectory, Key(request) + FailedExtension);

    private static string Key(ThumbnailRequest request) =>
        $"{request.LessonId:N}-{request.FileSize}-{request.LastWriteUtc.Ticks}";
}
