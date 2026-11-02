using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VibePlayer.App.ViewModels;

namespace VibePlayer.E2ETests;

public sealed class AccessibilityTests
{
    [AvaloniaFact]
    public void ElementosInterativos_TemNomeAcessivel()
    {
        var window = WindowTestExtensions.OpenMainWindow();
        var unnamed = new List<string>();

        // O TabControl só cria o conteúdo da aba selecionada; passa por todas.
        foreach (var tab in Enum.GetValues<SidePanelTab>())
        {
            window.ViewModel().SelectedTab = tab;
            WindowTestExtensions.Settle();

            // Só os controles declarados nas Views: partes internas de templates (ex.: os RepeatButtons
            // do trilho do Slider) não recebem foco nem são anunciadas como controles próprios.
            var interactive = window.GetVisualDescendants().OfType<Control>()
                .Where(c => c is Button or ToggleButton or TextBox or Slider or TabItem
                    && c.TemplatedParent is null
                    && c.IsEffectivelyVisible);

            unnamed.AddRange(
                from control in interactive
                where string.IsNullOrWhiteSpace(AccessibleName(control))
                select $"{control.GetType().Name} (aba {tab})");
        }

        unnamed.Distinct().ShouldBeEmpty();
        window.Close();
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
