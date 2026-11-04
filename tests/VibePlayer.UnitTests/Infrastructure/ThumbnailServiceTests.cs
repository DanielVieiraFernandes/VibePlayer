using VibePlayer.Core.Playback;
using VibePlayer.Infrastructure;
using VibePlayer.Infrastructure.Playback;

namespace VibePlayer.UnitTests.Infrastructure;

/// <summary>
/// Cache e fila de miniaturas com um gerador falso. Usa pasta temporária própria: o produto do
/// serviço são arquivos (mesma exceção dos testes de backup).
/// </summary>
public sealed class ThumbnailServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "VibePlayerTests", Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly RecordingGenerator _generator = new();
    private readonly ThumbnailService _service;

    public ThumbnailServiceTests()
    {
        _paths = new AppPaths(_root);
        _service = new ThumbnailService(_generator, _paths, new ListLogger<ThumbnailService>());
    }

    public void Dispose()
    {
        _service.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private static ThumbnailRequest Request(Guid? id = null, long size = 100, TimeSpan? duration = null) =>
        new(id ?? Guid.NewGuid(), "/cursos/aula.mp4", size, DateTime.UnixEpoch, duration ?? TimeSpan.FromMinutes(10));

    [Fact]
    public async Task Gerada_FicaNoCacheETiradaADezPorCento()
    {
        var request = Request();
        var ready = WaitReady(request.LessonId);

        _service.Request(request, ThumbnailPriority.Normal);
        await ready;

        _service.TryGetPath(request).ShouldNotBeNull();
        _generator.Calls.ShouldHaveSingleItem().At.ShouldBe(TimeSpan.FromMinutes(1));

        _service.Request(request, ThumbnailPriority.High);
        _generator.Calls.Count.ShouldBe(1, "Já existe: não gera de novo");
    }

    [Fact]
    public async Task ArquivoAlterado_GeraDeNovoEApagaAAntiga()
    {
        var id = Guid.NewGuid();
        var first = Request(id, size: 100);
        var ready = WaitReady(id);
        _service.Request(first, ThumbnailPriority.Normal);
        await ready;
        var oldPath = _service.TryGetPath(first)!;

        var changed = Request(id, size: 200);
        _service.TryGetPath(changed).ShouldBeNull();
        ready = WaitReady(id);
        _service.Request(changed, ThumbnailPriority.Normal);
        await ready;

        _service.TryGetPath(changed).ShouldNotBeNull();
        File.Exists(oldPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Falha_NaoTentaDeNovoParaAMesmaVersao()
    {
        _generator.Fail = true;
        var request = Request();

        _service.Request(request, ThumbnailPriority.Normal);
        await WaitUntil(() => Directory.GetFiles(_paths.ThumbnailsDirectory, "*.failed").Length == 1);
        var calls = _generator.Calls.Count;

        _service.Request(request, ThumbnailPriority.High);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        _generator.Calls.Count.ShouldBe(calls);
        calls.ShouldBe(2, "Tentou na posição e depois no começo do vídeo");
    }

    [Fact]
    public async Task Prioridade_SelecionadaPassaNaFrenteDaCapa()
    {
        _generator.Gate = new TaskCompletionSource();
        var busy = Request();
        var cover = Request();
        var selected = Request();

        // A primeira trava o gerador; enquanto isso, a capa e a selecionada entram na fila.
        _service.Request(busy, ThumbnailPriority.Normal);
        await WaitUntil(() => _generator.Calls.Count == 1);
        _service.Request(cover, ThumbnailPriority.Low);
        _service.Request(selected, ThumbnailPriority.High);
        var allDone = WaitReady(cover.LessonId);
        _generator.Gate.SetResult();
        await allDone;

        _generator.Calls.Select(c => c.LessonFile).ToList()
            .IndexOf(selected.LessonId.ToString("N")).ShouldBeLessThan(_generator.Calls.Select(c => c.LessonFile).ToList().IndexOf(cover.LessonId.ToString("N")));
    }

    private Task WaitReady(Guid lessonId)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.ThumbnailReady += (_, id) =>
        {
            if (id == lessonId)
            {
                done.TrySetResult();
            }
        };
        return done.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline);
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private sealed class RecordingGenerator : IThumbnailGenerator
    {
        public List<(string LessonFile, TimeSpan At)> Calls { get; } = [];

        public bool Fail { get; set; }

        public TaskCompletionSource? Gate { get; set; }

        public async Task<bool> GenerateAsync(string videoPath, TimeSpan at, string outputPath, CancellationToken cancellationToken = default)
        {
            lock (Calls)
            {
                Calls.Add((Path.GetFileNameWithoutExtension(outputPath).Split('-')[0], at));
            }

            if (Gate is { } gate)
            {
                await gate.Task;
            }

            if (Fail)
            {
                return false;
            }

            await File.WriteAllBytesAsync(outputPath, [1, 2, 3], cancellationToken);
            return true;
        }
    }
}
