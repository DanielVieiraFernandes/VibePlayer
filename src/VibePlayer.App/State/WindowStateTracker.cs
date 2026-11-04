using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using VibePlayer.App.ViewModels;
using VibePlayer.App.Views;
using VibePlayer.Core.Settings;
using VibePlayer.Core.Threading;

namespace VibePlayer.App.State;

/// <summary>
/// Lembra o layout da janela (tamanho, maximizada, painel lateral) entre aberturas.
/// </summary>
/// <remarks>
/// Fluxo:
/// <list type="number">
/// <item><see cref="LoadAsync"/> roda na inicialização, antes de a janela existir, para ela já
/// nascer no tamanho certo em vez de abrir no padrão e "pular".</item>
/// <item><see cref="Attach"/> aplica o estado e passa a observar mudanças.</item>
/// <item>Cada mudança reinicia um <see cref="DispatcherTimer"/>; só depois de
/// <see cref="SaveDelay"/> sem mudanças o estado é gravado. Arrastar a divisória gera dezenas
/// de mudanças por segundo e vira uma gravação só. O timer é da thread de UI, não um timer de
/// thread própria (ver regra de exceções no CLAUDE.md).</item>
/// <item>Ao fechar, o que estiver pendente é gravado na hora, para não perder a última mudança.</item>
/// </list>
/// </remarks>
public sealed class WindowStateTracker(ISettingsStore settings, ILogger<WindowStateTracker> logger)
{
    private static readonly TimeSpan CloseSaveTimeout = TimeSpan.FromSeconds(2);

    private static readonly HashSet<string> TrackedViewModelProperties =
    [
        nameof(MainWindowViewModel.IsSidePanelOpen),
        nameof(MainWindowViewModel.SidePanelWidth),
        nameof(MainWindowViewModel.SelectedTab),
    ];

    // O que veio do banco na inicialização, separado do estado vivo: toda janela nasce como o app
    // abriu, mesmo que outra janela já tenha mudado o layout (nos testes, várias por execução).
    private WindowLayoutState _loaded = WindowLayoutState.Default;
    private WindowLayoutState _state = WindowLayoutState.Default;
    private DispatcherTimer? _saveTimer;
    private bool _dirty;

    /// <summary>Espera depois da última mudança antes de gravar. Os testes encurtam.</summary>
    public TimeSpan SaveDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _loaded = new WindowLayoutState(
            await settings.GetAsync(UiSettings.SidePanelOpen, cancellationToken).ConfigureAwait(false),
            await settings.GetAsync(UiSettings.SidePanelWidth, cancellationToken).ConfigureAwait(false),
            await settings.GetAsync(UiSettings.SidePanelTab, cancellationToken).ConfigureAwait(false),
            await settings.GetAsync(UiSettings.WindowSize, cancellationToken).ConfigureAwait(false),
            await settings.GetAsync(UiSettings.WindowMaximized, cancellationToken).ConfigureAwait(false));
    }

    public void Attach(MainWindow window, MainWindowViewModel viewModel)
    {
        _state = _loaded;
        _dirty = false;
        viewModel.RestoreSidePanel(_state.SidePanelOpen, _state.SidePanelWidth, _state.SidePanelTab);
        window.Width = _state.Size.Width;
        window.Height = _state.Size.Height;
        window.WindowState = _state.Maximized ? WindowState.Maximized : WindowState.Normal;

        _saveTimer = new DispatcherTimer { Interval = SaveDelay };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveAsync().FireAndForget(logger, "SalvarEstadoDaJanela");
        };

        window.Opened += (_, _) => FitToScreen(window);
        viewModel.PropertyChanged += (_, e) => OnViewModelChanged(viewModel, e);
        window.PropertyChanged += (_, e) => OnWindowChanged(window, e);
        window.Closing += (_, _) => SavePendingNow();
    }

    private void OnViewModelChanged(MainWindowViewModel viewModel, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not null && TrackedViewModelProperties.Contains(e.PropertyName))
        {
            Update(_state with
            {
                SidePanelOpen = viewModel.IsSidePanelOpen,
                SidePanelWidth = viewModel.SidePanelWidth,
                SidePanelTab = viewModel.SelectedTab,
            });
        }
    }

    private void OnWindowChanged(Window window, AvaloniaPropertyChangedEventArgs e)
    {
        // Minimizada e tela cheia (do player) são estados passageiros: não viram preferência.
        if (e.Property == Window.WindowStateProperty && window.WindowState is WindowState.Normal or WindowState.Maximized)
        {
            Update(_state with { Maximized = window.WindowState == WindowState.Maximized });
        }
        else if (e.Property == TopLevel.ClientSizeProperty && window.WindowState == WindowState.Normal)
        {
            // Só o tamanho "restaurado" é guardado: desmaximizar volta ao tamanho que o usuário escolheu.
            Update(_state with { Size = new WindowSize(window.ClientSize.Width, window.ClientSize.Height) });
        }
    }

    private void Update(WindowLayoutState state)
    {
        if (state == _state)
        {
            return;
        }

        _state = state;
        _dirty = true;
        _saveTimer?.Stop();
        _saveTimer?.Start();
    }

    private async Task SaveAsync()
    {
        // Cópia antes dos awaits: mudanças que chegarem durante a gravação marcam de novo como
        // pendente e entram na próxima.
        var state = _state;
        _dirty = false;

        await settings.SetAsync(UiSettings.SidePanelOpen, state.SidePanelOpen).ConfigureAwait(false);
        await settings.SetAsync(UiSettings.SidePanelWidth, state.SidePanelWidth).ConfigureAwait(false);
        await settings.SetAsync(UiSettings.SidePanelTab, state.SidePanelTab).ConfigureAwait(false);
        await settings.SetAsync(UiSettings.WindowSize, state.Size).ConfigureAwait(false);
        await settings.SetAsync(UiSettings.WindowMaximized, state.Maximized).ConfigureAwait(false);
        logger.LogDebug("Estado da janela salvo");
    }

    private void SavePendingNow()
    {
        _saveTimer?.Stop();
        if (!_dirty)
        {
            return;
        }

        // Espera síncrona no Closing, que não pode ser assíncrono. Não trava a UI esperando ela
        // mesma porque SaveAsync e o store usam ConfigureAwait(false). O limite garante que o
        // app sempre fecha, mesmo com o disco travado; aí só a última mudança se perde.
        try
        {
            if (!SaveAsync().Wait(CloseSaveTimeout))
            {
                logger.LogWarning("Gravação do estado da janela excedeu {Timeout} ao fechar", CloseSaveTimeout);
            }
        }
        catch (AggregateException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Falha ao gravar o estado da janela ao fechar");
        }
    }

    private static void FitToScreen(Window window)
    {
        // Monitor menor que o da última vez (ex.: notebook fora da docking station).
        if (window.Screens.ScreenFromWindow(window) is not { } screen)
        {
            return;
        }

        var workingArea = screen.WorkingArea.Size.ToSize(screen.Scaling);
        if (window.Width > workingArea.Width || window.Height > workingArea.Height)
        {
            window.Width = Math.Min(window.Width, workingArea.Width);
            window.Height = Math.Min(window.Height, workingArea.Height);
        }
    }

    private sealed record WindowLayoutState(
        bool SidePanelOpen,
        double SidePanelWidth,
        SidePanelTab SidePanelTab,
        WindowSize Size,
        bool Maximized)
    {
        public static readonly WindowLayoutState Default = new(
            UiSettings.SidePanelOpen.Default,
            UiSettings.SidePanelWidth.Default,
            UiSettings.SidePanelTab.Default,
            UiSettings.WindowSize.Default,
            UiSettings.WindowMaximized.Default);
    }
}
