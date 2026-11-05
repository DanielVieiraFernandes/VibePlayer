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

    /// <summary>Padrão até a 0.2: 1280 × 800. Ver <see cref="WindowSizeOrDefault"/>.</summary>
    public static readonly WindowSize LegacyWindowSize = new(1280, 800);

    public static readonly SettingKey<WindowSize> WindowSize =
        new("ui.window.size", new WindowSize(1100, 700), size => size.Width > 0 && size.Height > 0);

    /// <summary>
    /// O tamanho é gravado ao fechar o app, então quem só abriu e fechou tem o padrão antigo salvo
    /// e nunca veria o novo. Tamanho salvo idêntico ao padrão antigo vale como "nunca redimensionou"
    /// (spec 0008); qualquer outro é escolha do usuário e fica.
    /// </summary>
    public static WindowSize WindowSizeOrDefault(WindowSize saved) =>
        saved == LegacyWindowSize ? WindowSize.Default : saved;

    public static readonly SettingKey<bool> WindowMaximized = new("ui.window.maximized", false);
}
