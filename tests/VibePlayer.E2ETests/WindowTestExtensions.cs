using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VibePlayer.App;
using VibePlayer.App.ViewModels;
using VibePlayer.App.Views;

namespace VibePlayer.E2ETests;

internal static class WindowTestExtensions
{
    /// <summary>Abre a janela principal real, no tamanho pedido, e espera o layout assentar.</summary>
    public static MainWindow OpenMainWindow(double width = 1280, double height = 800)
    {
        var window = ((VibePlayerApp)Application.Current!).CreateMainWindow();
        window.Width = width;
        window.Height = height;
        window.Show();
        Settle();
        return window;
    }

    /// <summary>Roda bindings, layout e o que estiver na fila do dispatcher.</summary>
    public static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    public static MainWindowViewModel ViewModel(this MainWindow window) => (MainWindowViewModel)window.DataContext!;

    public static T Find<T>(this Control root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    /// <summary>Se o texto está na tela (visível de fato, não só presente na árvore).</summary>
    public static bool ShowsText(this Control root, string text) =>
        root.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text && t.IsEffectivelyVisible);

    public static Rect BoundsIn(this Visual visual, Visual relativeTo) =>
        new(visual.TranslatePoint(default, relativeTo) ?? default, visual.Bounds.Size);

    public static void Click(this MainWindow window, Control control)
    {
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Settle();
    }

    public static void PressShortcut(this MainWindow window, PhysicalKey key)
    {
        // Mesmo modificador que o app registra (Ctrl, ou ⌘ no macOS).
        var modifiers = Application.Current!.PlatformSettings!.HotkeyConfiguration.CommandModifiers == KeyModifiers.Meta
            ? RawInputModifiers.Meta
            : RawInputModifiers.Control;
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Settle();
    }
}
