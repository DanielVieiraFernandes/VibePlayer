using VibePlayer.App.ViewModels;
using VibePlayer.Core.Settings;

namespace VibePlayer.App.State;

/// <summary>Tamanho da janela no estado normal (não maximizado), em pixels lógicos.</summary>
public sealed record WindowSize(double Width, double Height);

/// <summary>Preferências de layout lembradas entre aberturas (spec 0004).</summary>
public static class UiSettings
{
    public static readonly SettingKey<bool> SidePanelOpen = new("ui.sidePanel.open", true);

    public static readonly SettingKey<double> SidePanelWidth =
        new("ui.sidePanel.width", MainWindowViewModel.DefaultSidePanelWidth, width => width > 0);

    public static readonly SettingKey<SidePanelTab> SidePanelTab =
        new("ui.sidePanel.tab", ViewModels.SidePanelTab.Lessons, Enum.IsDefined);

    public static readonly SettingKey<WindowSize> WindowSize =
        new("ui.window.size", new WindowSize(1280, 800), size => size.Width > 0 && size.Height > 0);

    public static readonly SettingKey<bool> WindowMaximized = new("ui.window.maximized", false);
}
