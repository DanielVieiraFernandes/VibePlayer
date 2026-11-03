using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VibePlayer.App.ViewModels;
using VibePlayer.App.Views;
using VibePlayer.Core.Library;

namespace VibePlayer.E2ETests;

public sealed class PlayerShortcutTests : IDisposable
{
    private readonly PlayerHarness _app = new();

    public PlayerShortcutTests()
    {
        _app.Sample.AddVideo("Curso/1 - a.mp4");
        _app.Sample.AddVideo("Curso/2 - b.mp4");
    }

    public void Dispose() => _app.Dispose();

    private async Task<(MainWindow Window, PlayerViewModel Player)> OpenPlayingAsync()
    {
        var window = await _app.OpenAsync();
        await _app.AddFolderAsync("Curso", LibraryRootKind.Course);
        await window.ViewModel().Library.RefreshCommand.ExecuteAsync(null);
        await PlayerHarness.SelectAsync(window, "1 - a");
        _app.Player.SimulatePosition(TimeSpan.FromSeconds(60));

        // Foco no vídeo, como depois de clicar nele.
        window.Find<Border>("VideoArea").Focus();
        await PlayerHarness.SettleAsync();
        return (window, window.ViewModel().Player);
    }

    [AvaloniaFact]
    public async Task AtalhosDoPlayer_FuncionamComFocoNoVideo()
    {
        var (window, player) = await OpenPlayingAsync();

        window.PressKey(PhysicalKey.Space);
        player.IsPlaying.ShouldBeFalse();
        window.PressKey(PhysicalKey.K);
        player.IsPlaying.ShouldBeTrue();

        window.PressKey(PhysicalKey.ArrowRight);
        _app.Player.Position.ShouldBe(TimeSpan.FromSeconds(65));
        window.PressKey(PhysicalKey.J);
        _app.Player.Position.ShouldBe(TimeSpan.FromSeconds(55));
        window.PressKey(PhysicalKey.L);
        window.PressKey(PhysicalKey.ArrowLeft);
        _app.Player.Position.ShouldBe(TimeSpan.FromSeconds(60));

        window.PressKey(PhysicalKey.ArrowDown);
        player.Volume.ShouldBe(95);
        window.PressKey(PhysicalKey.ArrowUp);
        player.Volume.ShouldBe(100);

        window.PressKey(PhysicalKey.M);
        player.IsMuted.ShouldBeTrue();

        window.PressKey(PhysicalKey.Period, RawInputModifiers.Shift);
        player.Rate.ShouldBe(1.25);
        window.PressKey(PhysicalKey.Comma, RawInputModifiers.Shift);
        player.Rate.ShouldBe(1.0);

        window.PressKey(PhysicalKey.F);
        player.IsFullscreen.ShouldBeTrue();
        window.PressKey(PhysicalKey.Escape);
        player.IsFullscreen.ShouldBeFalse();

        window.PressShortcut(PhysicalKey.ArrowRight);
        await PlayerHarness.WaitUntilAsync(() => player.CurrentLesson?.Title == "2 - b", "próxima aula");
        window.PressShortcut(PhysicalKey.ArrowLeft);
        await PlayerHarness.WaitUntilAsync(() => player.CurrentLesson?.Title == "1 - a", "aula anterior");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Atalhos_NaoDisparamDigitandoNoChatNemNaArvore()
    {
        var (window, player) = await OpenPlayingAsync();

        // Árvore com foco: as setas são dela, não do player.
        var tree = window.Find<TreeView>("Tree");
        tree.GetVisualDescendants().OfType<TreeViewItem>().First().Focus();
        window.PressKey(PhysicalKey.ArrowRight);
        window.PressKey(PhysicalKey.M);
        _app.Player.Position.ShouldBe(TimeSpan.FromSeconds(60));
        player.IsMuted.ShouldBeFalse();

        // Campo de texto com foco: nem o Espaço vale.
        var input = new TextBox();
        window.Find<Grid>("ContentGrid").Children.Add(input);
        WindowTestExtensions.Settle();
        input.Focus();
        window.PressKey(PhysicalKey.Space);
        window.PressKey(PhysicalKey.K);
        player.IsPlaying.ShouldBeTrue();
        window.Close();
    }
}
