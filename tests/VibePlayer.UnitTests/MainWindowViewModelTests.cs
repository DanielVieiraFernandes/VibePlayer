using VibePlayer.App.ViewModels;

namespace VibePlayer.UnitTests;

public sealed class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateViewModel() => new();

    [Fact]
    public void Abertura_PainelAbertoNaAbaAulasComLarguraPadrao()
    {
        var vm = CreateViewModel();

        vm.IsSidePanelOpen.ShouldBeTrue();
        vm.SelectedTab.ShouldBe(SidePanelTab.Lessons);
        vm.SidePanelWidth.ShouldBe(MainWindowViewModel.DefaultSidePanelWidth);
    }

    [Fact]
    public void AlternarPainel_FechaEReabreMantendoALargura()
    {
        var vm = CreateViewModel();
        vm.ResizeSidePanel(50);

        vm.ToggleSidePanelCommand.Execute(null);
        vm.IsSidePanelOpen.ShouldBeFalse();

        vm.ToggleSidePanelCommand.Execute(null);
        vm.IsSidePanelOpen.ShouldBeTrue();
        vm.SidePanelWidth.ShouldBe(MainWindowViewModel.DefaultSidePanelWidth + 50);
    }

    [Theory]
    [InlineData(SidePanelTab.Chat)]
    [InlineData(SidePanelTab.KeyPoints)]
    public void MostrarAba_AbreOPainelNaAbaPedida(SidePanelTab tab)
    {
        var vm = CreateViewModel();
        vm.ToggleSidePanelCommand.Execute(null);

        vm.ShowTabCommand.Execute(tab);

        vm.IsSidePanelOpen.ShouldBeTrue();
        vm.SelectedTab.ShouldBe(tab);
        vm.SelectedTabIndex.ShouldBe((int)tab);
    }

    [Theory]
    [InlineData(-1000, MainWindowViewModel.MinSidePanelWidth)]
    [InlineData(1000, MainWindowViewModel.MaxSidePanelWidth)]
    public void Arrastar_RespeitaLimitesDoPainel(double delta, double expected)
    {
        var vm = CreateViewModel();

        vm.ResizeSidePanel(delta);

        vm.SidePanelWidth.ShouldBe(expected);
    }

    [Fact]
    public void JanelaEstreita_PainelEncolheParaOPlayerManterLarguraMinima()
    {
        var vm = CreateViewModel();
        vm.ResizeSidePanel(1000);

        vm.UpdateAvailableWidth(956);

        vm.SidePanelWidth.ShouldBe(956 - MainWindowViewModel.MinPlayerWidth);
    }
}
