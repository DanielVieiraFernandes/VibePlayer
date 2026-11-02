using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using VibePlayer.App.ViewModels;

namespace VibePlayer.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ContentGrid.SizeChanged += (_, e) => ViewModel?.UpdateAvailableWidth(e.NewSize.Width - SidePanelSplitter.Width);
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        RegisterShortcuts();
    }

    /// <summary>
    /// Atalhos criados em código, e não em XAML, porque o modificador muda por SO: Ctrl no Windows
    /// e no Linux, ⌘ no macOS. O HotkeyConfiguration da plataforma já sabe qual é o certo.
    /// </summary>
    private void RegisterShortcuts()
    {
        KeyBindings.Clear();
        if (ViewModel is not { } vm)
        {
            return;
        }

        var modifier = Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.B, modifier), Command = vm.ToggleSidePanelCommand });

        Key[] tabKeys = [Key.D1, Key.D2, Key.D3, Key.D4];
        foreach (var (key, tab) in tabKeys.Zip(Enum.GetValues<SidePanelTab>()))
        {
            KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(key, modifier), Command = vm.ShowTabCommand, CommandParameter = tab });
        }
    }

    private void OnSplitterDragDelta(object? sender, VectorEventArgs e) =>
        // O painel fica à direita da divisória: arrastar para a esquerda (X negativo) alarga.
        ViewModel?.ResizeSidePanel(-e.Vector.X);
}
