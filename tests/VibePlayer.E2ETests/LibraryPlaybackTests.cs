using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using VibePlayer.Core.Library;

namespace VibePlayer.E2ETests;

/// <summary>Durações e miniaturas na árvore da biblioteca (spec 0006).</summary>
public sealed class LibraryPlaybackTests : IDisposable
{
    private readonly PlayerHarness _app = new();

    public void Dispose() => _app.Dispose();

    [AvaloniaFact]
    public async Task Duracoes_EmSegundoPlano_ComPastaGrande()
    {
        for (var module = 1; module <= 20; module++)
        {
            for (var lesson = 1; lesson <= 100; lesson++)
            {
                _app.Sample.AddVideo($"Curso/Módulo {module}/Aula {lesson}.mp4");
            }
        }

        // Cada leitura demora: a UI não pode esperar por elas.
        _app.MediaInfo.Delay = TimeSpan.FromMilliseconds(2);
        var window = await _app.OpenAsync();
        await _app.AddFolderAsync("Curso", LibraryRootKind.Course);
        var library = window.ViewModel().Library;

        var refresh = library.RefreshCommand.ExecuteAsync(null);
        var uiResponded = false;
        Dispatcher.UIThread.Post(() => uiResponded = true);
        Dispatcher.UIThread.RunJobs();
        uiResponded.ShouldBeTrue();
        await refresh;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await PlayerHarness.WaitUntilAsync(
            () => library.Courses.Single().Node.AllLessons.All(l => l.Duration is not null),
            "todas as durações");
        TestContext.Current.SendDiagnosticMessage($"Durações em {stopwatch.Elapsed}");
        _app.MediaInfo.Reads.ShouldBe(2000);
        library.Courses.Single().Caption.ShouldEndWith("400 h 00 min");
        PlayerHarness.Lesson(window, "Aula 1").Caption.ShouldStartWith("12:00");
        window.Close();
    }

    [AvaloniaFact]
    public async Task MiniaturasEPlaceholder()
    {
        _app.Sample.AddVideo("Curso A/1 - x.mp4");
        _app.Sample.AddVideo("Curso A/2 - y.mp4");
        _app.Sample.AddVideo("Curso B/1 - z.mp4");
        var window = await _app.OpenAsync();
        await _app.AddFolderAsync("Curso A");
        await _app.AddFolderAsync("Curso B");
        var library = window.ViewModel().Library;
        await library.RefreshCommand.ExecuteAsync(null);

        // Capas dos dois cursos (primeira aula de cada); aulas de cursos fechados ainda não.
        await PlayerHarness.WaitUntilAsync(() => library.Courses.All(c => c.HasThumbnail), "capas");
        PlayerHarness.Lesson(window, "2 - y").HasThumbnail.ShouldBeFalse();
        _app.Thumbnails.Generated.Count.ShouldBe(2);

        // Abrir o curso pede as miniaturas das aulas dele.
        library.Courses.First(c => c.Title == "Curso A").IsExpanded = true;
        await PlayerHarness.WaitUntilAsync(() => PlayerHarness.Lesson(window, "2 - y").HasThumbnail, "miniatura da aula");

        // Vídeo alterado: miniatura nova.
        File.WriteAllBytes(_app.Sample.PathOf("Curso A/2 - y.mp4"), [1, 2, 3]);
        await library.RefreshCommand.ExecuteAsync(null);
        await PlayerHarness.WaitUntilAsync(() => _app.Thumbnails.Generated.Count(p => p.EndsWith("2 - y.mp4", StringComparison.Ordinal)) == 2, "miniatura refeita");
        window.Close();
    }

    [AvaloniaFact]
    public async Task MiniaturaQueFalha_FicaComIcone()
    {
        _app.Thumbnails.Fail = true;
        _app.Sample.AddVideo("Curso/1 - x.mp4");
        var window = await _app.OpenAsync();
        await _app.AddFolderAsync("Curso");
        var library = window.ViewModel().Library;
        await library.RefreshCommand.ExecuteAsync(null);
        library.Courses.Single().IsExpanded = true;
        await PlayerHarness.SettleAsync(500);

        PlayerHarness.Lesson(window, "1 - x").HasThumbnail.ShouldBeFalse();
        library.Courses.Single().HasThumbnail.ShouldBeFalse();
        window.Close();
    }
}
