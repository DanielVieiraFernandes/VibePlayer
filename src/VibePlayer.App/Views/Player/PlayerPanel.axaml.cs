using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VibePlayer.App.ViewModels;

namespace VibePlayer.App.Views.Player;

public partial class PlayerPanel : UserControl
{
    /// <summary>Em tela cheia, a barra e o cursor somem depois deste tempo com o ponteiro parado.</summary>
    public static readonly TimeSpan ControlsHideDelay = TimeSpan.FromSeconds(3);

    private readonly DispatcherTimer _hideTimer;
    private PlayerViewModel? _viewModel;

    public PlayerPanel()
    {
        InitializeComponent();
        _hideTimer = new DispatcherTimer { Interval = ControlsHideDelay };
        _hideTimer.Tick += (_, _) => HideControls();

        // Clicar no vídeo põe o foco no player: os atalhos simples (setas, letras) só agem com o
        // foco aqui, para não brigarem com a árvore da biblioteca (spec 0006, "Atalhos").
        VideoArea.PointerPressed += (_, _) => VideoArea.Focus();
        VideoArea.DoubleTapped += (_, _) => _viewModel?.ToggleFullscreenCommand.Execute(null);
        PointerMoved += (_, _) => ShowControls();

        // Barra compacta em players estreitos: sem anterior/próxima e sem o controle deslizante de
        // volume (o botão de mudo e as setas ↑/↓ continuam). Assim tudo cabe nos 400 px mínimos.
        SizeChanged += (_, e) => Root.Classes.Set("compact", e.NewSize.Width < CompactWidth);
    }

    /// <summary>Abaixo desta largura a barra do player fica compacta.</summary>
    public const double CompactWidth = 560;

    /// <summary>Se a barra está visível (os testes de tela cheia conferem).</summary>
    public bool AreControlsVisible => ControlsBar.Opacity > 0;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
        }

        _viewModel = DataContext as PlayerViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelChanged;
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerViewModel.IsFullscreen) or nameof(PlayerViewModel.IsPlaying))
        {
            ShowControls();
        }
    }

    private void ShowControls()
    {
        ControlsBar.Opacity = 1;
        ControlsBar.IsHitTestVisible = true;
        VideoArea.Cursor = Cursor.Default;

        // Só some em tela cheia e tocando: pausado, a barra fica (o usuário está decidindo algo).
        _hideTimer.Stop();
        if (_viewModel is { IsFullscreen: true, IsPlaying: true })
        {
            _hideTimer.Start();
        }
    }

    private void HideControls()
    {
        _hideTimer.Stop();
        if (_viewModel is not { IsFullscreen: true, IsPlaying: true } || ControlsBar.IsPointerOver)
        {
            return;
        }

        ControlsBar.Opacity = 0;
        ControlsBar.IsHitTestVisible = false;
        VideoArea.Cursor = new Cursor(StandardCursorType.None);
    }

    // Dica com o tempo do ponto sob o ponteiro na barra de posição.
    private void OnPositionPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewModel is not { DurationSeconds: > 0 } viewModel || sender is not Slider slider || slider.Bounds.Width <= 0)
        {
            return;
        }

        var fraction = Math.Clamp(e.GetPosition(slider).X / slider.Bounds.Width, 0, 1);
        var time = TimeSpan.FromSeconds(fraction * viewModel.DurationSeconds);
        ToolTip.SetTip(slider, time.ToString(time.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss", CultureInfo.InvariantCulture));
        ToolTip.SetIsOpen(slider, true);
    }
}
