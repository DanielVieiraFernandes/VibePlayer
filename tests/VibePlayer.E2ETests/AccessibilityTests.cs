using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using VibePlayer.App;
using VibePlayer.App.Resources;
using VibePlayer.App.Services;
using VibePlayer.App.ViewModels;
using VibePlayer.App.Views;

namespace VibePlayer.E2ETests;

public sealed class AccessibilityTests
{
    [AvaloniaFact]
    public void ElementosInterativos_TemNomeAcessivel()
    {
        var window = WindowTestExtensions.OpenMainWindow();

        UnnamedControlsInAllTabs(window).ShouldBeEmpty();
        window.Close();
    }

    [AvaloniaFact]
    public async Task ElementosInterativos_ComBibliotecaPreenchida_TemNomeAcessivel()
    {
        using var data = new TemporaryAppData();
        using var sample = new SampleLibrary();
        sample.AddVideo("Curso/Módulo 1/a.mp4");
        sample.AddVideo("Curso/b.mp4");
        var picker = new FakeFilePicker();
        picker.Folders.Enqueue(sample.PathOf("Curso"));
        using var services = data.BuildServices(overrides =>
        {
            overrides.AddSingleton<IFilePicker>(picker);
            overrides.AddSingleton<IDialogService>(new ScriptedDialogs());
        });
        AppStartup.Prepare(services);
        var window = new VibePlayerApp(services).CreateMainWindow();
        window.Show();
        await window.ViewModel().Library.AddFolderCommand.ExecuteAsync(null);
        WindowTestExtensions.Settle();

        foreach (var item in window.GetVisualDescendants().OfType<TreeViewItem>().ToList())
        {
            item.IsExpanded = true;
        }

        WindowTestExtensions.Settle();
        window.GetVisualDescendants().OfType<TreeViewItem>().Count().ShouldBeGreaterThan(2);
        UnnamedControlsInAllTabs(window).ShouldBeEmpty();
        window.Close();
    }

    [AvaloniaFact]
    public async Task Player_ControlesTemNomeEEstadoEhAnunciado()
    {
        using var app = new PlayerHarness();
        app.Sample.AddVideo("Curso/1 - a.mp4");
        var window = await app.OpenAsync();
        await app.AddFolderAsync("Curso");
        await window.ViewModel().Library.RefreshCommand.ExecuteAsync(null);
        await PlayerHarness.SelectAsync(window, "1 - a");

        UnnamedControlsInAllTabs(window).ShouldBeEmpty();

        // Botões que mudam de função mudam de nome; o estado vai para a região anunciada.
        var playPause = window.Find<Button>("PlayPauseButton");
        var liveStatus = window.Find<TextBlock>("LiveStatus");
        AutomationProperties.GetName(playPause).ShouldBe(Strings.Player_Pause);
        AutomationProperties.GetLiveSetting(liveStatus).ShouldBe(AutomationLiveSetting.Polite);
        liveStatus.Text.ShouldBe(Strings.Player_StatusPlaying);

        window.ViewModel().Player.PlayPauseCommand.Execute(null);
        await PlayerHarness.SettleAsync();
        AutomationProperties.GetName(playPause).ShouldBe(Strings.Player_Play);
        liveStatus.Text.ShouldBe(Strings.Player_StatusPaused);
        window.Close();
    }

    private static List<string> UnnamedControlsInAllTabs(MainWindow window)
    {
        var unnamed = new List<string>();

        // O TabControl só cria o conteúdo da aba selecionada; passa por todas.
        foreach (var tab in Enum.GetValues<SidePanelTab>())
        {
            window.ViewModel().SelectedTab = tab;
            WindowTestExtensions.Settle();

            // Só os controles declarados nas Views: partes internas de templates (ex.: os RepeatButtons
            // do trilho do Slider) não recebem foco nem são anunciadas como controles próprios.
            var interactive = window.GetVisualDescendants().OfType<Control>()
                .Where(c => c is Button or ToggleButton or TextBox or Slider or TabItem or TreeViewItem
                    && c.TemplatedParent is null
                    && c.IsEffectivelyVisible);

            unnamed.AddRange(
                from control in interactive
                where string.IsNullOrWhiteSpace(AccessibleName(control))
                select $"{control.GetType().Name} (aba {tab})");
        }

        return unnamed.Distinct().ToList();
    }

    // Leitores de tela usam o AutomationProperties.Name ou, na falta dele, o texto do conteúdo.
    private static string? AccessibleName(Control control) =>
        AutomationProperties.GetName(control) is { Length: > 0 } name
            ? name
            : control switch
            {
                HeaderedContentControl { Header: string header } => header,
                ContentControl { Content: string text } => text,
                _ => null,
            };
}
