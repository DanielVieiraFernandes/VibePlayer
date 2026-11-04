using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VibePlayer.App.ViewModels;

namespace VibePlayer.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ContentGrid.SizeChanged += (_, e) => ViewModel?.UpdateAvailableWidth(e.NewSize.Width - SidePanelSplitter.Width);

        // Bolha, e não túnel: controles que tratam a tecla (botão com foco no Espaço, setas na
        // árvore, digitação no chat) marcam como tratada e o atalho não dispara por cima.
        AddHandler(KeyDownEvent, OnPlayerShortcut, RoutingStrategies.Bubble);
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        RegisterShortcuts();
        if (ViewModel is { } vm)
        {
            vm.Player.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(PlayerViewModel.IsFullscreen))
                {
                    ApplyFullscreen(vm.Player.IsFullscreen);
                }
            };
        }
    }

    private WindowState _stateBeforeFullscreen = WindowState.Normal;

    private void ApplyFullscreen(bool fullscreen)
    {
        if (fullscreen)
        {
            _stateBeforeFullscreen = WindowState;
            WindowState = WindowState.FullScreen;
        }
        else if (WindowState == WindowState.FullScreen)
        {
            WindowState = _stateBeforeFullscreen;
        }
    }

    /// <summary>
    /// Atalhos do player (spec 0006). Regras de foco:
    /// <list type="bullet">
    /// <item>com um campo de texto em foco, nenhum atalho simples dispara (o usuário está digitando);</item>
    /// <item>teclas simples (setas, letras) só agem com o foco no player ou em lugar nenhum, porque
    /// na árvore da biblioteca as setas navegam;</item>
    /// <item>Espaço, Esc e os combinados com Ctrl/⌘ valem em qualquer lugar fora de campo de texto.</item>
    /// </list>
    /// </summary>
    private void OnPlayerShortcut(object? sender, KeyEventArgs e)
    {
        if (e.Handled || ViewModel?.Player is not { } player)
        {
            return;
        }

        var focused = FocusManager?.GetFocusedElement() as Visual;
        if (focused is TextBox)
        {
            return;
        }

        var commandModifier = Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        var playerFocused = focused is null || focused == this || PlayerPanel.IsVisualAncestorOf(focused) || focused == PlayerPanel;

        e.Handled = (e.Key, e.KeyModifiers) switch
        {
            (Key.Space, KeyModifiers.None) => Run(() => player.PlayPauseCommand.Execute(null)),
            (Key.Escape, KeyModifiers.None) when player.IsFullscreen => Run(() => player.ExitFullscreenCommand.Execute(null)),
            (Key.Right, var m) when m == commandModifier => Run(() => player.NextLessonCommand.Execute(null)),
            (Key.Left, var m) when m == commandModifier => Run(() => player.PreviousLessonCommand.Execute(null)),
            _ when !playerFocused || e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift) => false,
            (Key.K, KeyModifiers.None) => Run(() => player.PlayPauseCommand.Execute(null)),
            (Key.Left, KeyModifiers.None) => Run(() => player.SeekShort(forward: false)),
            (Key.Right, KeyModifiers.None) => Run(() => player.SeekShort(forward: true)),
            (Key.J, KeyModifiers.None) => Run(() => player.SeekLong(forward: false)),
            (Key.L, KeyModifiers.None) => Run(() => player.SeekLong(forward: true)),
            (Key.Up, KeyModifiers.None) => Run(() => player.ChangeVolume(up: true)),
            (Key.Down, KeyModifiers.None) => Run(() => player.ChangeVolume(up: false)),
            (Key.M, KeyModifiers.None) => Run(() => player.ToggleMuteCommand.Execute(null)),
            (Key.F, KeyModifiers.None) => Run(() => player.ToggleFullscreenCommand.Execute(null)),
            (Key.OemPeriod, KeyModifiers.Shift) => Run(() => player.ChangeRate(faster: true)),
            (Key.OemComma, KeyModifiers.Shift) => Run(() => player.ChangeRate(faster: false)),
            _ => false,
        };

        static bool Run(Action action)
        {
            action();
            return true;
        }
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

        Key[] tabKeys = [Key.D1, Key.D2, Key.D3, Key.D4, Key.D5];
        foreach (var (key, tab) in tabKeys.Zip(Enum.GetValues<SidePanelTab>()))
        {
            KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(key, modifier), Command = vm.ShowTabCommand, CommandParameter = tab });
        }
    }

    private void OnSplitterDragDelta(object? sender, VectorEventArgs e) =>
        // O painel fica à direita da divisória: arrastar para a esquerda (X negativo) alarga.
        ViewModel?.ResizeSidePanel(-e.Vector.X);
}
