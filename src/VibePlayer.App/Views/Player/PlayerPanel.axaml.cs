using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VibePlayer.App.ViewModels;

namespace VibePlayer.App.Views.Player;

public partial class PlayerPanel : UserControl
{
    /// <summary>Em tela cheia, a barra e o cursor somem depois deste tempo com o ponteiro parado.</summary>
    public static readonly TimeSpan ControlsHideDelay = TimeSpan.FromSeconds(3);

    /// <summary>Plano B se a plataforma não informar o intervalo de duplo clique.</summary>
    private static readonly TimeSpan DefaultClickDelay = TimeSpan.FromMilliseconds(400);

    private readonly DispatcherTimer _hideTimer;
    private readonly DispatcherTimer _clickTimer = new() { Interval = DefaultClickDelay };
    private const double SeekPreviewGap = 8;

    private PlayerViewModel? _viewModel;
    private bool _isScrubbing;

    public PlayerPanel()
    {
        InitializeComponent();
        _hideTimer = new DispatcherTimer { Interval = ControlsHideDelay };
        _hideTimer.Tick += (_, _) => HideControls();

        // Clicar no vídeo põe o foco no player: os atalhos simples (setas, letras) só agem com o
        // foco aqui, para não brigarem com a árvore da biblioteca (spec 0006, "Atalhos").
        VideoArea.PointerPressed += (_, _) => VideoArea.Focus();
        VideoArea.Tapped += OnVideoTapped;
        VideoArea.DoubleTapped += (_, _) =>
        {
            // O primeiro clique do par só agenda a pausa; o duplo cancela, para entrar na tela
            // cheia sem mexer na reprodução.
            _clickTimer.Stop();
            _viewModel?.ToggleFullscreenCommand.Execute(null);
        };
        _clickTimer.Tick += (_, _) =>
        {
            _clickTimer.Stop();
            if (_viewModel is { } viewModel && viewModel.PlayPauseCommand.CanExecute(null))
            {
                viewModel.PlayPauseCommand.Execute(null);
            }
        };
        PointerMoved += (_, _) => ShowControls();

        // Barra compacta em players estreitos: sem anterior/próxima e sem o controle deslizante de
        // volume (o botão de mudo e as setas ↑/↓ continuam). Assim tudo cabe nos 400 px mínimos.
        SizeChanged += (_, e) => Root.Classes.Set("compact", e.NewSize.Width < CompactWidth);

        // Túnel e handledEventsToo: o Slider trata (e marca) os eventos de ponteiro para mover o
        // Thumb, e aqui precisamos vê-los antes dele para saber quando o arraste começa.
        PositionSlider.AddHandler(PointerPressedEvent, OnPositionPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        PositionSlider.AddHandler(PointerReleasedEvent, OnPositionReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        PositionSlider.AddHandler(PointerCaptureLostEvent, (_, _) => EndScrub(), RoutingStrategies.Bubble, handledEventsToo: true);
        PositionSlider.AddHandler(PointerMovedEvent, (_, e) => UpdateSeekPreview(e), RoutingStrategies.Tunnel, handledEventsToo: true);
        PositionSlider.PointerExited += (_, _) => HideSeekPreview();
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

    /// <summary>
    /// Clique no vídeo alterna pausa/reprodução, mas só depois do intervalo de duplo clique da
    /// plataforma: se vier um segundo clique, a intenção era a tela cheia e a pausa é descartada.
    /// Botões sobre o vídeo (play do pôster, contagem da próxima aula) tratam o próprio clique.
    /// </summary>
    private void OnVideoTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        _clickTimer.Interval = Application.Current?.PlatformSettings?.GetDoubleTapTime(e.Pointer?.Type ?? PointerType.Mouse)
            ?? DefaultClickDelay;
        _clickTimer.Stop();
        _clickTimer.Start();
    }

    private void OnPositionPressed(object? sender, PointerPressedEventArgs e)
    {
        _isScrubbing = true;
        _viewModel?.BeginScrub();
    }

    private void OnPositionReleased(object? sender, PointerReleasedEventArgs e)
    {
        EndScrub();
        if (!PositionSlider.IsPointerOver)
        {
            HideSeekPreview();
        }
    }

    private void EndScrub()
    {
        _isScrubbing = false;
        _viewModel?.EndScrub();
    }

    /// <summary>
    /// Mostra, em cima do ponteiro, o tempo daquele ponto da barra. Um ToolTip não serve: ele abre
    /// uma vez e não acompanha o mouse.
    /// </summary>
    private void UpdateSeekPreview(PointerEventArgs e)
    {
        if (_viewModel is not { DurationSeconds: > 0 } viewModel || PositionSlider.Bounds.Width <= 0)
        {
            return;
        }

        var thumbWidth = PositionSlider.GetVisualDescendants().OfType<Thumb>().FirstOrDefault()?.Bounds.Width ?? 0;
        var fraction = SeekFraction(e.GetPosition(PositionSlider).X, PositionSlider.Bounds.Width, thumbWidth);
        SeekPreviewText.Text = PlayerViewModel.FormatTime(TimeSpan.FromSeconds(fraction * viewModel.DurationSeconds));
        SeekPreview.IsVisible = true;

        SeekPreview.Measure(Size.Infinity);
        var size = SeekPreview.DesiredSize;
        var barTop = ControlsBar.TranslatePoint(default, Root)?.Y ?? Root.Bounds.Height;
        var pointerX = e.GetPosition(Root).X;
        Canvas.SetLeft(SeekPreview, Math.Clamp(pointerX - (size.Width / 2), 0, Math.Max(0, Root.Bounds.Width - size.Width)));
        Canvas.SetTop(SeekPreview, barTop - size.Height - SeekPreviewGap);
    }

    private void HideSeekPreview()
    {
        if (!_isScrubbing)
        {
            SeekPreview.IsVisible = false;
        }
    }

    /// <summary>
    /// Fração da barra sob o ponteiro, com a mesma conta do Slider ao clicar: o centro do Thumb só
    /// anda entre meia largura dele e o fim menos meia largura. Assim o tempo mostrado é o mesmo
    /// para onde o clique leva.
    /// </summary>
    public static double SeekFraction(double x, double sliderWidth, double thumbWidth)
    {
        var track = sliderWidth - thumbWidth;
        return track <= 0 ? 0 : Math.Clamp((x - (thumbWidth / 2)) / track, 0, 1);
    }
}
