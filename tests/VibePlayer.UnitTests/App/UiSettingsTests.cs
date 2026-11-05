using VibePlayer.App.State;

namespace VibePlayer.UnitTests.App;

public sealed class UiSettingsTests
{
    [Fact]
    public void TamanhoPadrao_ENovoEMenorQueOAntigo()
    {
        UiSettings.WindowSize.Default.ShouldBe(new WindowSize(1100, 700));
        UiSettings.WindowSize.Default.Width.ShouldBeLessThan(UiSettings.LegacyWindowSize.Width);
    }

    [Fact]
    public void TamanhoSalvoIgualAoPadraoAntigo_ViraOPadraoNovo()
    {
        UiSettings.WindowSizeOrDefault(new WindowSize(1280, 800)).ShouldBe(UiSettings.WindowSize.Default);
    }

    [Theory]
    [InlineData(1000, 650)]
    [InlineData(1280, 801)]
    [InlineData(1920, 1080)]
    public void TamanhoEscolhidoPeloUsuario_Permanece(double width, double height)
    {
        var chosen = new WindowSize(width, height);
        UiSettings.WindowSizeOrDefault(chosen).ShouldBe(chosen);
    }
}
