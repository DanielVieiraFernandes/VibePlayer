using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VibePlayer.App.Views;
using VibePlayer.App.Views.Player;

namespace VibePlayer.E2ETests;

/// <summary>Clique no vídeo, duplo clique e tela cheia (spec 0008, CA4 a CA7).</summary>
public sealed class VideoClickTests : IDisposable
{
    private readonly PlayerHarness _app = new();

    public VideoClickTests()
    {
        _app.Sample.AddVideo("Curso/1 - Abertura.mp4");
        _app.Sample.AddVideo("Curso/2 - Meio.mp4");
    }

    public void Dispose() => _app.Dispose();

    private async Task<MainWindow> OpenPlayingAsync()
    {
        var window = await _app.OpenAsync();
        await _app.AddFolderAsync("Curso");
        await window.ViewModel().Library.RefreshCommand.ExecuteAsync(null);
        await PlayerHarness.SettleAsync();
        await PlayerHarness.SelectAsync(window, "1 - Abertura");
        return window;
    }

    private static Point TopThirdOfVideo(MainWindow window)
    {
        var area = window.Find<Border>("VideoArea");
        return area.TranslatePoint(new Point(area.Bounds.Width / 2, area.Bounds.Height / 3), window)!.Value;
    }

    private static void ClickAt(MainWindow window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    /// <summary>Espera mais que o intervalo de duplo clique, para o clique simples agir.</summary>
    private static Task WaitClickDelayAsync() => PlayerHarness.SettleAsync(900);

    [AvaloniaFact]
    public async Task CliqueNoVideo_PausaEDepoisReproduz()
    {
        var window = await OpenPlayingAsync();
        var player = window.ViewModel().Player;
        player.IsPlaying.ShouldBeTrue();

        ClickAt(window, TopThirdOfVideo(window));
        await WaitClickDelayAsync();
        player.IsPlaying.ShouldBeFalse("Um clique pausa");

        ClickAt(window, TopThirdOfVideo(window));
        await WaitClickDelayAsync();
        player.IsPlaying.ShouldBeTrue("O clique seguinte reproduz");
        window.Close();
    }

    [AvaloniaFact]
    public async Task DuploCliqueNoVideo_AlternaTelaCheiaSemPausar()
    {
        var window = await OpenPlayingAsync();
        var player = window.ViewModel().Player;
        var point = TopThirdOfVideo(window);

        // Segundo clique logo em seguida, na mesma posição.
        ClickAt(window, point);
        ClickAt(window, point);
        await WaitClickDelayAsync();

        player.IsFullscreen.ShouldBeTrue();
        player.IsPlaying.ShouldBeTrue("O duplo clique não pausa");
        window.Close();
    }

    [AvaloniaFact]
    public async Task CliqueNaBarraDeControles_NaoPassaPeloVideo()
    {
        var window = await OpenPlayingAsync();
        var player = window.ViewModel().Player;

        // Texto do tempo na barra: não é vídeo e não pausa.
        window.Click(window.Find<TextBlock>("PositionText"));
        await WaitClickDelayAsync();
        player.IsPlaying.ShouldBeTrue();

        // O botão de pausa age uma vez só (não soma o clique do vídeo).
        window.Click(window.Find<Button>("PlayPauseButton"));
        await WaitClickDelayAsync();
        player.IsPlaying.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public async Task SemAula_CliqueNoVideoNaoFazNada()
    {
        var window = await _app.OpenAsync();
        var player = window.ViewModel().Player;

        ClickAt(window, TopThirdOfVideo(window));
        await WaitClickDelayAsync();

        player.HasLesson.ShouldBeFalse();
        player.IsPlaying.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public async Task TelaCheia_BarraSobrepostaEVideoOcupaTudoComBarraVisivelOuEscondida()
    {
        var window = await OpenPlayingAsync();
        var player = window.ViewModel().Player;
        var panel = window.GetVisualDescendants().OfType<PlayerPanel>().Single();
        var area = window.Find<Border>("VideoArea");
        var bar = window.Find<Border>("ControlsBar");

        // Fora da tela cheia a barra fica abaixo do vídeo, sem cobri-lo.
        bar.BoundsIn(window).Top.ShouldBeGreaterThanOrEqualTo(area.BoundsIn(window).Bottom - 0.5);

        player.ToggleFullscreenCommand.Execute(null);
        await PlayerHarness.SettleAsync();

        area.Bounds.Width.ShouldBe(panel.Bounds.Width, 0.5);
        area.Bounds.Height.ShouldBe(panel.Bounds.Height, 0.5, "Vídeo na altura toda mesmo com a barra visível");
        bar.BoundsIn(window).Top.ShouldBeLessThan(area.BoundsIn(window).Bottom, "A barra cobre o vídeo");
        bar.BoundsIn(window).Bottom.ShouldBe(area.BoundsIn(window).Bottom, 0.5);

        await PlayerHarness.SettleAsync((int)PlayerPanel.ControlsHideDelay.TotalMilliseconds + 500);
        panel.AreControlsVisible.ShouldBeFalse();
        area.Bounds.Height.ShouldBe(panel.Bounds.Height, 0.5, "Sem faixa reservada com a barra escondida");
        window.Close();
    }
}
