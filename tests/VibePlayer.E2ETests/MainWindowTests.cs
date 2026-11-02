using Avalonia;
using Avalonia.Headless.XUnit;
using VibePlayer.App;

namespace VibePlayer.E2ETests;

public sealed class MainWindowTests
{
    [AvaloniaFact]
    public void Inicializacao_AbreJanelaComTituloDoApp()
    {
        var app = (VibePlayerApp)Application.Current!;

        var window = app.CreateMainWindow();
        window.Show();

        window.IsVisible.ShouldBeTrue();
        window.Title.ShouldBe("VibePlayer");

        window.Close();
    }
}
