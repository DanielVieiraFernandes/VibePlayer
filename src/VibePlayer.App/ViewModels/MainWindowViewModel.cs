using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VibePlayer.Core.Threading;

namespace VibePlayer.App.ViewModels;

/// <summary>
/// Shell da janela: player à esquerda e painel lateral (Aulas, Chat, Resumo, Pontos principais)
/// à direita, que pode ser recolhido e redimensionado.
/// </summary>
/// <remarks>
/// A largura do painel mora aqui, e não na ColumnDefinition da View, porque recolher e reabrir
/// precisa lembrar a última largura usada; um GridSplitter grava larguras fixas direto na coluna
/// e apagaria esse valor. A View só aplica <see cref="SidePanelWidth"/> e repassa o arraste.
/// </remarks>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    public const double DefaultSidePanelWidth = 380;
    public const double MinSidePanelWidth = 320;
    public const double MaxSidePanelWidth = 560;
    public const double MinPlayerWidth = 400;

    private readonly ILogger<MainWindowViewModel> _logger;
    private double _availableWidth = double.PositiveInfinity;

    /// <summary>Usado apenas pelo previewer de XAML.</summary>
    public MainWindowViewModel()
        : this(NullLogger<MainWindowViewModel>.Instance, new LibraryViewModel(), new PlayerViewModel(), new AssistantViewModel())
    {
    }

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        LibraryViewModel library,
        PlayerViewModel player,
        AssistantViewModel assistant)
    {
        _logger = logger;
        Library = library;
        Player = player;
        Assistant = assistant;
        IsSidePanelOpen = true;
        SidePanelWidth = DefaultSidePanelWidth;
        SelectedTab = SidePanelTab.Lessons;

        // A aula escolhida na biblioteca vai para o player; a que o player começa sozinho (próxima
        // aula, reabertura) fica marcada na biblioteca. Os dois lados ignoram a aula que já têm,
        // então a ida e a volta não viram um ciclo.
        library.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LibraryViewModel.CurrentLesson) && library.CurrentLesson is { } lesson)
            {
                player.LoadAsync(lesson.Id, autoPlay: true).FireAndForget(_logger, "AbrirAula");
            }
        };
        player.LessonStarted += (_, lessonId) => library.ShowCurrentLesson(lessonId);
        player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerViewModel.IsFullscreen))
            {
                OnPropertyChanged(nameof(IsFullscreen));
                OnPropertyChanged(nameof(ShowSidePanel));
            }
        };
        player.LessonCompleted += (_, _) => library.ReloadInBackground();
    }

    /// <summary>
    /// Depois que a janela abre: preferências do player, biblioteca e, por fim, a última aula
    /// assistida, pausada (spec 0006).
    /// </summary>
    public async Task InitializeAsync()
    {
        await Player.LoadPreferencesAsync();
        await Library.InitializeAsync();
        if (await Player.GetLastLessonIdAsync() is { } lastLesson)
        {
            await Player.LoadAsync(lastLesson, autoPlay: false);
        }
    }

    public string Title => Resources.Strings.AppName;

    /// <summary>Tela cheia: só o player, sem barra superior nem painel lateral.</summary>
    public bool IsFullscreen => Player.IsFullscreen;

    public bool ShowSidePanel => IsSidePanelOpen && !IsFullscreen;

        public LibraryViewModel Library { get; }

    public PlayerViewModel Player { get; }

    public AssistantViewModel Assistant { get; }

    [ObservableProperty]
    public partial bool IsSidePanelOpen { get; set; }

    [ObservableProperty]
    public partial double SidePanelWidth { get; set; }

    [ObservableProperty]
    public partial SidePanelTab SelectedTab { get; set; }

    /// <summary>Índice para o TabControl, que trabalha com SelectedIndex.</summary>
    public int SelectedTabIndex
    {
        get => (int)SelectedTab;
        set => SelectedTab = (SidePanelTab)value;
    }

    [RelayCommand]
    private void ToggleSidePanel() => IsSidePanelOpen = !IsSidePanelOpen;

    /// <summary>Abre o painel já na aba pedida (atalhos Ctrl+1 a Ctrl+4).</summary>
    [RelayCommand]
    private void ShowTab(SidePanelTab tab)
    {
        SelectedTab = tab;
        IsSidePanelOpen = true;
    }

    /// <summary>Restaura o estado salvo da última abertura; a largura volta aos limites atuais.</summary>
    public void RestoreSidePanel(bool isOpen, double width, SidePanelTab tab)
    {
        IsSidePanelOpen = isOpen;
        SidePanelWidth = ClampWidth(width);
        SelectedTab = tab;
    }

    /// <summary>Aplica o arraste da divisória; positivo alarga o painel.</summary>
    public void ResizeSidePanel(double delta) => SidePanelWidth = ClampWidth(SidePanelWidth + delta);

    /// <summary>
    /// Informa a largura disponível para player + painel. Chamado quando a janela muda de tamanho,
    /// para o painel encolher e o player nunca ficar abaixo de <see cref="MinPlayerWidth"/>.
    /// </summary>
    public void UpdateAvailableWidth(double width)
    {
        _availableWidth = width;
        SidePanelWidth = ClampWidth(SidePanelWidth);
    }

    private double ClampWidth(double width)
    {
        // O limite do player vence o máximo do painel, mas nunca o mínimo: abaixo de 320 px as
        // abas não cabem. A largura mínima da janela (960) garante os dois mínimos (400 + 320).
        var max = Math.Max(MinSidePanelWidth, Math.Min(MaxSidePanelWidth, _availableWidth - MinPlayerWidth));
        return Math.Clamp(width, MinSidePanelWidth, max);
    }

    partial void OnIsSidePanelOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSidePanel));
        _logger.LogDebug("Painel lateral {State}", value ? "aberto" : "fechado");
    }

    partial void OnSelectedTabChanged(SidePanelTab value)
    {
        OnPropertyChanged(nameof(SelectedTabIndex));
        _logger.LogDebug("Aba do painel lateral: {Tab}", value);
    }
}
