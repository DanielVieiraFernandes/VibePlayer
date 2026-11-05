using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VibePlayer.App.Resources;
using VibePlayer.App.ViewModels;
using VibePlayer.App.Views.Shell;

namespace VibePlayer.E2ETests;

/// <summary>Abas do painel lateral só com ícones (spec 0008, CA8).</summary>
public sealed class SidePanelTabsTests
{
    private static readonly string[] Names =
    [
        Strings.Tab_Lessons, Strings.Tab_Transcription, Strings.Tab_Chat, Strings.Tab_Summary, Strings.Tab_KeyPoints,
    ];

    [AvaloniaFact]
    public void CincoAbas_CabemEmUmaLinhaNaLarguraMinimaDoPainel()
    {
        var window = WindowTestExtensions.OpenMainWindow(width: 960, height: 600);
        window.ViewModel().ResizeSidePanel(-1000);
        WindowTestExtensions.Settle();

        var sidePanel = window.Find<SidePanel>("SidePanel");
        sidePanel.Bounds.Width.ShouldBe(320, 0.5);

        var tabs = sidePanel.GetVisualDescendants().OfType<TabItem>().ToList();
        tabs.Count.ShouldBe(5);
        tabs.Select(t => t.BoundsIn(window).Top).Distinct().Count().ShouldBe(1, "Uma linha só");
        foreach (var tab in tabs)
        {
            tab.BoundsIn(window).Right.ShouldBeLessThanOrEqualTo(sidePanel.BoundsIn(window).Right + 0.5);
        }

        window.Close();
    }

    [AvaloniaFact]
    public void CadaAba_TemNomeAcessivelEDica()
    {
        var window = WindowTestExtensions.OpenMainWindow();
        var tabs = window.Find<SidePanel>("SidePanel").GetVisualDescendants().OfType<TabItem>().ToList();

        tabs.Select(AutomationProperties.GetName).ShouldBe(Names);
        tabs.Select(t => ToolTip.GetTip(t)).ShouldBe(Names);
        window.Close();
    }

    [AvaloniaFact]
    public void AbaAtiva_TemTituloVisivelEEstadoSelecionado()
    {
        var window = WindowTestExtensions.OpenMainWindow();
        var vm = window.ViewModel();
        var sidePanel = window.Find<SidePanel>("SidePanel");

        foreach (var (tab, index) in Enum.GetValues<SidePanelTab>().Select((t, i) => (t, i)))
        {
            vm.SelectedTab = tab;
            WindowTestExtensions.Settle();
            sidePanel.Find<TextBlock>("ActiveTabTitle").Text.ShouldBe(Names[index]);
            var tabs = sidePanel.GetVisualDescendants().OfType<TabItem>().ToList();
            tabs.Where(t => t.IsSelected).ShouldHaveSingleItem().ShouldBe(tabs[index]);
        }

        window.Close();
    }
}
