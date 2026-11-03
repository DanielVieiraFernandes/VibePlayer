using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using VibePlayer.App.Controls;
using VibePlayer.App.Resources;
using VibePlayer.App.ViewModels;
using VibePlayer.App.Views;
using VibePlayer.App.Views.Player;
using VibePlayer.App.Views.Shell;
using VibePlayer.Core.Playback;

namespace VibePlayer.E2ETests;

public sealed class PlayerTests : IDisposable
{
    private readonly PlayerHarness _app = new();

    public PlayerTests()
    {
        _app.Sample.AddVideo("Curso/1 - Abertura.mp4");
        _app.Sample.AddVideo("Curso/Módulo/2 - Meio.mp4");
        _app.Sample.AddVideo("Curso/Módulo/3 - Fim.mp4");
    }

    public void Dispose() => _app.Dispose();

    private async Task<MainWindow> OpenWithCourseAsync()
    {
        var window = await _app.OpenAsync();
        await _app.AddFolderAsync("Curso");
        await window.ViewModel().Library.RefreshCommand.ExecuteAsync(null);
        await PlayerHarness.SettleAsync();
        return window;
    }

    [AvaloniaFact]
    public async Task SelecionarAula_TocaEHabilitaControles()
    {
        var window = await OpenWithCourseAsync();
        var player = window.ViewModel().Player;

        await PlayerHarness.SelectAsync(window, "1 - Abertura");

        _app.Player.Opened.ShouldHaveSingleItem().AutoPlay.ShouldBeTrue();
        player.IsPlaying.ShouldBeTrue();
        player.HasLesson.ShouldBeTrue();
        player.DurationText.ShouldBe("10:00");
        window.Find<Border>("ControlsBar").IsEffectivelyEnabled.ShouldBeTrue();
        window.Find<Button>("PlayPauseButton").GetValue(Avalonia.Automation.AutomationProperties.NameProperty).ShouldBe(Strings.Player_Pause);
        window.GetVisualDescendants().OfType<VideoView>().Single().HasFrame.ShouldBeTrue();

        window.Find<Button>("PlayPauseButton").Command!.Execute(null);
        await PlayerHarness.SettleAsync();
        player.IsPlaying.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public async Task Seek_VolumeEVelocidade_SobrevivemAReabertura()
    {
        var window = await OpenWithCourseAsync();
        await PlayerHarness.SelectAsync(window, "1 - Abertura");
        var player = window.ViewModel().Player;

        player.PositionSeconds = 120;
        _app.Player.Position.ShouldBe(TimeSpan.FromSeconds(120), "Arrastar a barra leva o vídeo à posição");
        player.SeekLong(forward: true);
        _app.Player.Position.ShouldBe(TimeSpan.FromSeconds(130));
        player.Volume = 40;
        player.IsMuted = true;
        player.SetRateCommand.Execute(1.5);
        _app.Player.Volume.ShouldBe(40);
        _app.Player.Rate.ShouldBe(1.5);
        await PlayerHarness.SettleAsync(300);
        window.Close();

        var reopened = await _app.OpenAsync();
        var restored = reopened.ViewModel().Player;
        restored.Volume.ShouldBe(40);
        restored.IsMuted.ShouldBeTrue();
        restored.Rate.ShouldBe(1.5);
        restored.RateText.ShouldBe(1.5.ToString("0.##", CultureInfo.CurrentCulture) + "×");
        reopened.Close();
    }

    [AvaloniaFact]
    public async Task Posicao_SalvaERetomada()
    {
        var window = await OpenWithCourseAsync();
        await PlayerHarness.SelectAsync(window, "2 - Meio");
        _app.Player.SimulatePosition(TimeSpan.FromSeconds(95));
        await PlayerHarness.SettleAsync();

        // Trocar de aula salva a posição da anterior.
        await PlayerHarness.SelectAsync(window, "3 - Fim");
        await PlayerHarness.SelectAsync(window, "2 - Meio");

        _app.Player.Opened[^1].StartAt.ShouldBe(TimeSpan.FromSeconds(95));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Concluir_AtualizaBibliotecaEMarcarDesmarcar()
    {
        var window = await OpenWithCourseAsync();
        await PlayerHarness.SelectAsync(window, "1 - Abertura");

        _app.Player.SimulatePosition(FakeMediaPlayer.DefaultDuration * 0.96);
        await PlayerHarness.WaitUntilAsync(() => PlayerHarness.Lesson(window, "1 - Abertura").IsCompleted, "concluída");

        var course = window.ViewModel().Library.Courses.Single();
        course.Caption.ShouldStartWith(string.Format(Strings.Library_CourseProgress, 1, 3));

        // Desmarcar pelo menu da aula.
        await window.ViewModel().Library.ToggleCompletedCommand.ExecuteAsync(PlayerHarness.Lesson(window, "1 - Abertura"));
        PlayerHarness.Lesson(window, "1 - Abertura").IsCompleted.ShouldBeFalse();
        await window.ViewModel().Library.ToggleCompletedCommand.ExecuteAsync(PlayerHarness.Lesson(window, "3 - Fim"));
        PlayerHarness.Lesson(window, "3 - Fim").IsCompleted.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public async Task FimDaAula_ContagemEProxima()
    {
        var window = await OpenWithCourseAsync();
        await PlayerHarness.SelectAsync(window, "1 - Abertura");
        var player = window.ViewModel().Player;

        _app.Player.SimulateEnd();
        await PlayerHarness.WaitUntilAsync(() => player.IsCountingDown, "contagem");
        window.ShowsText(string.Format(Strings.Player_NextLessonIn, PlayerViewModel.CountdownSeconds)).ShouldBeTrue();

        // Atravessa o módulo: a próxima de "1 - Abertura" é "2 - Meio".
        await PlayerHarness.WaitUntilAsync(() => player.CurrentLesson?.Title == "2 - Meio", "próxima aula");
        _app.Player.Opened[^1].AutoPlay.ShouldBeTrue();
        window.ViewModel().Library.CurrentLesson!.Title.ShouldBe("2 - Meio");
        window.Close();
    }

    [AvaloniaFact]
    public async Task CancelarContagem_MantemAulaENaUltimaNadaAcontece()
    {
        var window = await OpenWithCourseAsync();
        await PlayerHarness.SelectAsync(window, "2 - Meio");
        var player = window.ViewModel().Player;

        _app.Player.SimulateEnd();
        await PlayerHarness.WaitUntilAsync(() => player.IsCountingDown, "contagem");
        player.CancelCountdownCommand.Execute(null);
        await PlayerHarness.SettleAsync(1500);
        player.CurrentLesson!.Title.ShouldBe("2 - Meio");

        await PlayerHarness.SelectAsync(window, "3 - Fim");
        _app.Player.SimulateEnd();
        await PlayerHarness.SettleAsync(500);
        player.IsCountingDown.ShouldBeFalse("Última aula do curso: não há próxima");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Reabertura_SelecionaUltimaAulaPausadaComPoster()
    {
        var window = await OpenWithCourseAsync();
        await PlayerHarness.SelectAsync(window, "2 - Meio");
        _app.Player.SimulatePosition(TimeSpan.FromSeconds(42));
        window.ViewModel().Player.SaveBeforeClose();
        window.Close();

        var reopened = await _app.OpenAsync();
        var player = reopened.ViewModel().Player;
        await PlayerHarness.WaitUntilAsync(() => player.CurrentLesson is not null, "aula reaberta");
        await PlayerHarness.SettleAsync(300);

        _app.Player.Opened.ShouldHaveSingleItem().ShouldBe((_app.Sample.PathOf("Curso/Módulo/2 - Meio.mp4"), TimeSpan.FromSeconds(42), false));
        player.IsPlaying.ShouldBeFalse();
        player.ShowPoster.ShouldBeTrue();
        reopened.ViewModel().Library.CurrentLesson!.Title.ShouldBe("2 - Meio");
        reopened.Close();
    }

    [AvaloniaFact]
    public async Task ErroAoAbrir_MostraMensagemESegueFuncionando()
    {
        var window = await OpenWithCourseAsync();
        await PlayerHarness.SelectAsync(window, "1 - Abertura");

        _app.Player.SimulateError();
        await PlayerHarness.SettleAsync();

        window.ShowsText(Strings.Player_ErrorTitle).ShouldBeTrue();
        window.ShowsText(Strings.Player_ErrorCannotPlay).ShouldBeTrue();
        await PlayerHarness.SelectAsync(window, "2 - Meio");
        window.ViewModel().Player.IsPlaying.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public async Task VlcAusente_MostraOrientacao()
    {
        var window = await OpenWithCourseAsync();
        _app.Player.Availability = PlayerAvailability.Unavailable;

        await PlayerHarness.SelectAsync(window, "1 - Abertura");

        window.ShowsText(Strings.Player_UnavailableTitle).ShouldBeTrue();
        _app.Player.Opened.ShouldBeEmpty();
        window.ViewModel().Library.HasCourses.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public async Task AulaPausada_MostraPosterComMiniatura()
    {
        var window = await OpenWithCourseAsync();
        await PlayerHarness.SelectAsync(window, "1 - Abertura");
        await PlayerHarness.WaitUntilAsync(() => window.ViewModel().Player.Poster is not null, "pôster");
        window.Close();
    }

    [AvaloniaFact]
    public async Task TelaCheia_SoOPlayer_ControlesSomemEVoltam()
    {
        var window = await OpenWithCourseAsync();
        await PlayerHarness.SelectAsync(window, "1 - Abertura");
        var player = window.ViewModel().Player;
        var panel = window.GetVisualDescendants().OfType<PlayerPanel>().Single();

        player.ToggleFullscreenCommand.Execute(null);
        await PlayerHarness.SettleAsync();

        window.WindowState.ShouldBe(WindowState.FullScreen);
        window.Find<SidePanel>("SidePanel").IsVisible.ShouldBeFalse();
        panel.AreControlsVisible.ShouldBeTrue();

        await PlayerHarness.SettleAsync((int)PlayerPanel.ControlsHideDelay.TotalMilliseconds + 500);
        panel.AreControlsVisible.ShouldBeFalse("Some com o ponteiro parado");

        window.MouseMove(new Avalonia.Point(200, 200));
        await PlayerHarness.SettleAsync();
        panel.AreControlsVisible.ShouldBeTrue("Volta ao mexer o ponteiro");

        window.PressKey(Avalonia.Input.PhysicalKey.Escape);
        window.WindowState.ShouldNotBe(WindowState.FullScreen);
        window.Find<SidePanel>("SidePanel").IsVisible.ShouldBeTrue();
        window.Close();
    }
}
