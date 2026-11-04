using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VibePlayer.App.Resources;
using VibePlayer.App.ViewModels;
using VibePlayer.App.Views.Player;
using VibePlayer.App.Views.Shell;

namespace VibePlayer.E2ETests;

public sealed class MainWindowLayoutTests
{
    private static readonly (SidePanelTab Tab, string EmptyTitle)[] TabEmptyStates =
    [
        (SidePanelTab.Lessons, Strings.Library_EmptyTitle),
        (SidePanelTab.Transcription, Strings.Transcription_NoLessonTitle),
        (SidePanelTab.Chat, Strings.Chat_EmptyTitle),
        (SidePanelTab.Summary, Strings.Summary_EmptyTitle),
        (SidePanelTab.KeyPoints, Strings.KeyPoints_EmptyTitle),
    ];

    [AvaloniaFact]
    public void Abertura_MostraRegioesComEstadosVazios()
    {
        var window = WindowTestExtensions.OpenMainWindow();
        var player = window.Find<PlayerPanel>("PlayerPanel");
        var sidePanel = window.Find<SidePanel>("SidePanel");

        window.GetVisualDescendants().OfType<TopBar>().Single().IsEffectivelyVisible.ShouldBeTrue();
        player.IsEffectivelyVisible.ShouldBeTrue();
        sidePanel.IsEffectivelyVisible.ShouldBeTrue();
        player.BoundsIn(window).Right.ShouldBeLessThanOrEqualTo(sidePanel.BoundsIn(window).Left);

        window.ShowsText(Strings.Player_EmptyTitle).ShouldBeTrue();
        window.ViewModel().SelectedTab.ShouldBe(SidePanelTab.Lessons);

        foreach (var (tab, emptyTitle) in TabEmptyStates)
        {
            window.ViewModel().SelectedTab = tab;
            WindowTestExtensions.Settle();
            window.ShowsText(emptyTitle).ShouldBeTrue($"Aba {tab} sem estado vazio");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void BotaoEAtalho_AlternamPainelLateral()
    {
        var window = WindowTestExtensions.OpenMainWindow();
        var player = window.Find<PlayerPanel>("PlayerPanel");
        var sidePanel = window.Find<SidePanel>("SidePanel");
        var playerWidthWithPanel = player.Bounds.Width;

        window.Click(window.Find<ToggleButton>("SidePanelToggle"));

        sidePanel.IsVisible.ShouldBeFalse();
        player.Bounds.Width.ShouldBeGreaterThan(playerWidthWithPanel);
        player.Bounds.Width.ShouldBe(window.Find<Grid>("ContentGrid").Bounds.Width, 0.5);

        window.PressShortcut(PhysicalKey.B);

        sidePanel.IsVisible.ShouldBeTrue();
        player.Bounds.Width.ShouldBe(playerWidthWithPanel, 0.5);

        window.Close();
    }

    [AvaloniaFact]
    public void AtalhosDeAba_AbremPainelNaAbaPedida()
    {
        var window = WindowTestExtensions.OpenMainWindow();
        var sidePanel = window.Find<SidePanel>("SidePanel");
        PhysicalKey[] keys = [PhysicalKey.Digit1, PhysicalKey.Digit2, PhysicalKey.Digit3, PhysicalKey.Digit4, PhysicalKey.Digit5];

        foreach (var ((tab, emptyTitle), key) in TabEmptyStates.Zip(keys))
        {
            window.ViewModel().IsSidePanelOpen = false;
            WindowTestExtensions.Settle();

            window.PressShortcut(key);

            sidePanel.IsVisible.ShouldBeTrue();
            window.ViewModel().SelectedTab.ShouldBe(tab);
            window.ShowsText(emptyTitle).ShouldBeTrue($"Atalho {key} não mostrou a aba {tab}");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void TamanhoMinimo_SemCortes()
    {
        var window = WindowTestExtensions.OpenMainWindow(width: 960, height: 600);
        // Pior caso: painel arrastado até o máximo.
        window.ViewModel().ResizeSidePanel(1000);
        WindowTestExtensions.Settle();

        var player = window.Find<PlayerPanel>("PlayerPanel");
        var sidePanel = window.Find<SidePanel>("SidePanel");

        player.Bounds.Width.ShouldBeGreaterThanOrEqualTo(MainWindowViewModel.MinPlayerWidth);
        sidePanel.BoundsIn(window).Right.ShouldBeLessThanOrEqualTo(window.Bounds.Width + 0.5);

        // Os controles do player cabem na barra e as quatro abas cabem numa linha só.
        var playerBounds = player.BoundsIn(window);
        foreach (var button in player.GetVisualDescendants().OfType<Button>())
        {
            button.BoundsIn(window).Right.ShouldBeLessThanOrEqualTo(playerBounds.Right + 0.5);
        }

        var tabTops = sidePanel.GetVisualDescendants().OfType<TabItem>().Select(t => t.BoundsIn(window).Top).Distinct();
        tabTops.Count().ShouldBe(1);

        window.Close();
    }
}
