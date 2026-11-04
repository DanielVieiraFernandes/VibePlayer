using System.Globalization;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VibePlayer.App.Resources;
using VibePlayer.App.Services;
using VibePlayer.App.State;
using VibePlayer.Core.Playback;
using VibePlayer.Core.Settings;
using VibePlayer.Core.Threading;

namespace VibePlayer.App.ViewModels;

/// <summary>
/// Área do vídeo e barra de controles: abre a aula escolhida, salva o progresso e passa para a
/// próxima aula (spec 0006).
/// </summary>
/// <remarks>
/// Fluxo de uma aula:
/// <list type="number">
/// <item><see cref="LoadAsync"/> salva a posição da aula anterior, calcula onde a nova começa
/// (<see cref="PlaybackRules.ResumePosition"/>) e abre o arquivo.</item>
/// <item>Enquanto toca, a posição é salva a cada 5 s de avanço (sem timer: medido pelos próprios
/// avisos de posição), ao pausar, ao trocar de aula e ao fechar o app.</item>
/// <item>Chegar a 95% marca como concluída (uma vez); o fim do vídeo também, e começa a contagem
/// para a próxima aula disponível do curso.</item>
/// </list>
/// Os avisos do player chegam em threads do VLC e são repassados para a thread de UI antes de
/// qualquer coisa; nunca se chama o player de dentro de um aviso dele (ver VlcMediaPlayer).
/// </remarks>
public sealed partial class PlayerViewModel : ViewModelBase
{
    public const int CountdownSeconds = 5;
    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ShortSeek = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LongSeek = TimeSpan.FromSeconds(10);
    private const int VolumeStep = 5;

    // Durante o arraste, o vídeo acompanha a barra no máximo neste ritmo: cada seek obriga o VLC a
    // decodificar a partir do quadro-chave anterior, e pedir um por movimento do mouse o afoga.
    private static readonly TimeSpan ScrubSeekInterval = TimeSpan.FromMilliseconds(200);

    // Depois de um seek, avisos de posição antigos ainda chegam por um instante; sem esperar, a
    // barra voltaria ao ponto anterior antes de pular para o novo.
    private static readonly TimeSpan SeekSettleTime = TimeSpan.FromSeconds(1);
    private const double SeekSettleToleranceSeconds = 1.5;

    private readonly IMediaPlayer? _player;
    private readonly ILessonProgressStore? _progress;
    private readonly ISettingsStore? _settings;
    private readonly IThumbnailService? _thumbnails;
    private readonly ThumbnailImages? _images;
    private readonly ILogger<PlayerViewModel> _logger;

    private PlayableLesson? _lesson;
    private Guid? _requestedLessonId;
    private TimeSpan _lastSavedPosition;
    private bool _completedThisSession;
    private bool _autoAdvance = true;
    private DispatcherTimer? _countdownTimer;
    private int _uiUpdateScheduled;
    private bool _updatingFromPlayer;
    private DispatcherTimer? _scrubTimer;
    private bool _isScrubbing;
    private double? _pendingScrubSeek;
    private double? _seekTarget;
    private long _seekSettleDeadline;

    /// <summary>Usado apenas pelo previewer de XAML.</summary>
    public PlayerViewModel()
    {
        _logger = NullLogger<PlayerViewModel>.Instance;
    }

    public PlayerViewModel(
        IMediaPlayer player,
        ILessonProgressStore progress,
        ISettingsStore settings,
        IThumbnailService thumbnails,
        ThumbnailImages images,
        ILogger<PlayerViewModel> logger)
    {
        _player = player;
        _progress = progress;
        _settings = settings;
        _thumbnails = thumbnails;
        _images = images;
        _logger = logger;

        player.StateChanged += (_, _) => Dispatcher.UIThread.Post(OnStateChanged);
        player.DurationChanged += (_, _) => Dispatcher.UIThread.Post(OnPositionOrDurationChanged);

        // A posição muda dezenas de vezes por segundo: no máximo uma atualização agendada por vez.
        player.PositionChanged += (_, _) =>
        {
            if (Interlocked.Exchange(ref _uiUpdateScheduled, 1) == 0)
            {
                Dispatcher.UIThread.Post(OnPositionOrDurationChanged);
            }
        };
    }

    /// <summary>A aula começou a tocar (inclusive pela passagem automática), para a biblioteca acompanhar.</summary>
    public event EventHandler<Guid>? LessonStarted;

    /// <summary>A aula foi marcada como concluída (ícone da biblioteca muda).</summary>
    public event EventHandler<Guid>? LessonCompleted;

    public IVideoFrameSource? Video => _player?.Video;

    public IReadOnlyList<double> Rates => PlayerRates.All;

    /// <summary>Aula carregada (título e curso, para o estado vazio e o pôster).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedLesson), nameof(HasLesson), nameof(ShowEmptyState))]
    public partial SelectedLesson? CurrentLesson { get; private set; }

    public bool HasSelectedLesson => CurrentLesson is not null;

    /// <summary>Controles habilitados: há aula aberta e o player funciona.</summary>
    public bool HasLesson => CurrentLesson is not null && IsAvailable && State is not PlaybackState.Error and not PlaybackState.Idle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaying), nameof(PlayPauseName), nameof(HasLesson), nameof(ShowPoster), nameof(ShowVideo), nameof(IsLoading), nameof(HasError), nameof(ErrorText), nameof(StatusText))]
    public partial PlaybackState State { get; private set; }

    public bool IsPlaying => State == PlaybackState.Playing;

    public bool IsLoading => State == PlaybackState.Opening;

    public bool HasError => State == PlaybackState.Error;

    public string? ErrorText => !HasError
        ? null
        : _player?.Error == PlaybackError.FileNotFound ? Strings.Player_ErrorFileNotFound : Strings.Player_ErrorCannotPlay;

    /// <summary>VLC presente (no Linux vem do sistema e pode faltar).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLesson), nameof(IsUnavailable), nameof(ShowEmptyState))]
    public partial bool IsAvailable { get; private set; } = true;

    public bool IsUnavailable => !IsAvailable;

    public bool ShowEmptyState => CurrentLesson is null && IsAvailable;

    /// <summary>Aula aberta e ainda sem tocar nenhuma vez: mostra a miniatura no lugar do vídeo.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPoster), nameof(ShowVideo))]
    public partial bool HasStartedPlaying { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPoster))]
    public partial Bitmap? Poster { get; private set; }

    public bool ShowPoster => CurrentLesson is not null && !HasStartedPlaying && !HasError && IsAvailable;

    public bool ShowVideo => HasStartedPlaying && !HasError;

    /// <summary>Texto anunciado aos leitores de tela quando o estado muda.</summary>
    public string StatusText => State switch
    {
        PlaybackState.Playing => Strings.Player_StatusPlaying,
        PlaybackState.Paused => Strings.Player_StatusPaused,
        PlaybackState.Ended => Strings.Player_StatusEnded,
        PlaybackState.Error => ErrorText ?? string.Empty,
        _ => string.Empty,
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionText))]
    public partial double PositionSeconds { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationText))]
    public partial double DurationSeconds { get; private set; }

    public string PositionText => FormatTime(TimeSpan.FromSeconds(PositionSeconds));

    public string DurationText => FormatTime(TimeSpan.FromSeconds(DurationSeconds));

    [ObservableProperty]
    public partial int Volume { get; set; } = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MuteName))]
    public partial bool IsMuted { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RateText))]
    public partial double Rate { get; set; } = 1.0;

    public string RateText => Rate.ToString("0.##", CultureInfo.CurrentCulture) + "×";

    public string PlayPauseName => IsPlaying ? Strings.Player_Pause : Strings.Player_Play;

    public string MuteName => IsMuted ? Strings.Player_Unmute : Strings.Player_Mute;

    public string FullscreenName => IsFullscreen ? Strings.Player_ExitFullscreen : Strings.Player_Fullscreen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullscreenName))]
    public partial bool IsFullscreen { get; set; }

    /// <summary>Segundos até a próxima aula; nulo sem contagem em andamento.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCountingDown), nameof(CountdownText))]
    public partial int? CountdownRemaining { get; private set; }

    public bool IsCountingDown => CountdownRemaining is not null;

    public string CountdownText => CountdownRemaining is { } seconds
        ? string.Format(CultureInfo.CurrentCulture, Strings.Player_NextLessonIn, seconds)
        : string.Empty;

    /// <summary>Carrega volume, mudo e velocidade salvos.</summary>
    public async Task LoadPreferencesAsync()
    {
        if (_settings is null || _player is null)
        {
            return;
        }

        Volume = await _settings.GetAsync(PlayerSettings.Volume);
        IsMuted = await _settings.GetAsync(PlayerSettings.Muted);
        Rate = await _settings.GetAsync(PlayerSettings.Rate);
        _autoAdvance = await _settings.GetAsync(PlayerSettings.AutoAdvance);
    }

    /// <summary>A última aula assistida, para a reabertura do app.</summary>
    public async Task<Guid?> GetLastLessonIdAsync() =>
        _settings is null ? null : await _settings.GetAsync(PlayerSettings.LastLessonId);

    /// <summary>
    /// Abre a aula. <paramref name="autoPlay"/> falso abre pausada na posição salva (reabertura do
    /// app: nunca começar a tocar som de repente).
    /// </summary>
    public async Task LoadAsync(Guid lessonId, bool autoPlay)
    {
        if (_player is null || _progress is null)
        {
            return;
        }

        // A mesma aula de novo (a biblioteca acompanhando o que o player abriu) não reabre. Decide
        // pela aula pedida, marcada antes de qualquer await: o pedido repetido costuma chegar
        // enquanto a primeira abertura ainda está no meio. Com erro, escolher de novo é tentar de novo.
        if (_requestedLessonId == lessonId && State != PlaybackState.Error)
        {
            return;
        }

        _requestedLessonId = lessonId;

        CancelCountdown();
        await SaveCurrentPositionAsync();

        IsAvailable = _player.Availability == PlayerAvailability.Available;
        if (await _progress.GetPlayableAsync(lessonId) is not { } lesson)
        {
            _requestedLessonId = _lesson?.Id;
            return;
        }

        _lesson = lesson;
        CurrentLesson = new SelectedLesson(lesson.Id, lesson.Title, lesson.CourseTitle);
        HasStartedPlaying = false;
        Poster = null;
        LoadPosterAsync(lesson).FireAndForget(_logger, "CarregarPoster");

        if (!IsAvailable)
        {
            return;
        }

        var saved = await _progress.GetAsync(lesson.Id);
        var start = PlaybackRules.ResumePosition(saved, lesson.Duration);
        _lastSavedPosition = start;
        _completedThisSession = saved?.IsCompleted ?? false;
        PositionSeconds = start.TotalSeconds;
        DurationSeconds = lesson.Duration?.TotalSeconds ?? 0;

        _player.Open(lesson.FilePath, start, autoPlay);
        _logger.LogInformation("Reprodução iniciada: {LessonId} em {StartMs}", lesson.Id, (long)start.TotalMilliseconds);
        await (_settings?.SetAsync(PlayerSettings.LastLessonId, lesson.Id) ?? Task.CompletedTask);
        LessonStarted?.Invoke(this, lesson.Id);
    }

    /// <summary>Grava a posição atual (app fechando). Síncrono por causa do evento de fechamento.</summary>
    public void SaveBeforeClose()
    {
        CancelCountdown();
        try
        {
            if (!SaveCurrentPositionAsync().Wait(TimeSpan.FromSeconds(2)))
            {
                _logger.LogWarning("Gravação da posição excedeu o limite ao fechar");
            }
        }
        catch (AggregateException ex)
        {
            _logger.LogError(ex.InnerException ?? ex, "Falha ao gravar a posição ao fechar");
        }

        _player?.Stop();
    }

    [RelayCommand]
    private void PlayPause()
    {
        if (_player is null || !HasLesson)
        {
            return;
        }

        CancelCountdown();
        if (IsPlaying)
        {
            _player.Pause();
        }
        else
        {
            _player.Play();
        }
    }

    [RelayCommand]
    private void SeekBy(double seconds)
    {
        if (_player is null || !HasLesson)
        {
            return;
        }

        var target = _player.Position + TimeSpan.FromSeconds(seconds);
        SeekTo((target < TimeSpan.Zero ? TimeSpan.Zero : target).TotalSeconds);
    }

    /// <summary>
    /// Começo do arraste na barra de posição. Daqui até <see cref="EndScrub"/> a barra segue só o
    /// ponteiro (avisos do player são ignorados, senão ela brigaria com o mouse) e o vídeo
    /// acompanha em intervalos de <see cref="ScrubSeekInterval"/>.
    /// </summary>
    public void BeginScrub()
    {
        if (!HasLesson)
        {
            return;
        }

        _isScrubbing = true;
        CancelCountdown();
        if (_scrubTimer is null)
        {
            _scrubTimer = new DispatcherTimer { Interval = ScrubSeekInterval };
            _scrubTimer.Tick += (_, _) => ApplyPendingScrubSeek();
        }

        _scrubTimer.Start();
    }

    /// <summary>Soltou a barra: o vídeo vai exatamente para onde ela parou.</summary>
    public void EndScrub()
    {
        if (!_isScrubbing)
        {
            return;
        }

        _isScrubbing = false;
        _scrubTimer?.Stop();
        _pendingScrubSeek = null;
        SeekTo(PositionSeconds);
    }

    /// <summary>Vai a um ponto do vídeo, como um clique num tempo da transcrição.</summary>
    public void SeekToTime(TimeSpan position) => SeekTo(position.TotalSeconds);

    public void SeekShort(bool forward) => SeekBy((forward ? ShortSeek : -ShortSeek).TotalSeconds);

    public void SeekLong(bool forward) => SeekBy((forward ? LongSeek : -LongSeek).TotalSeconds);

    public void ChangeVolume(bool up) => Volume = Math.Clamp(Volume + (up ? VolumeStep : -VolumeStep), 0, 100);

    [RelayCommand]
    private void ToggleMute() => IsMuted = !IsMuted;

    [RelayCommand]
    private void SetRate(double rate) => Rate = rate;

    public void ChangeRate(bool faster)
    {
        var index = Array.IndexOf(PlayerRates.All, Rate);
        index = Math.Clamp((index < 0 ? Array.IndexOf(PlayerRates.All, 1.0) : index) + (faster ? 1 : -1), 0, PlayerRates.All.Length - 1);
        Rate = PlayerRates.All[index];
    }

    [RelayCommand]
    private void ToggleFullscreen()
    {
        if (CurrentLesson is not null || IsFullscreen)
        {
            IsFullscreen = !IsFullscreen;
        }
    }

    [RelayCommand]
    private void ExitFullscreen() => IsFullscreen = false;

    [RelayCommand]
    private Task NextLessonAsync() => GoToNeighborAsync(next: true);

    [RelayCommand]
    private Task PreviousLessonAsync() => GoToNeighborAsync(next: false);

    [RelayCommand]
    private void CancelCountdown()
    {
        _countdownTimer?.Stop();
        _countdownTimer = null;
        CountdownRemaining = null;
    }

    partial void OnPositionSecondsChanged(double value)
    {
        // Mudança vinda da barra (arraste ou clique): só vira seek se for longe da posição real;
        // as atualizações do próprio player chegam aqui também e não podem virar seek.
        if (_updatingFromPlayer || _player is null || !HasLesson)
        {
            return;
        }

        if (_isScrubbing)
        {
            _pendingScrubSeek = value;
        }
        else if (Math.Abs(value - _player.Position.TotalSeconds) > 0.5)
        {
            SeekTo(value);
        }
    }

    partial void OnVolumeChanged(int value)
    {
        _player?.Volume = value;
        Save(PlayerSettings.Volume, value);
    }

    partial void OnIsMutedChanged(bool value)
    {
        _player?.IsMuted = value;
        Save(PlayerSettings.Muted, value);
    }

    partial void OnRateChanged(double value)
    {
        _player?.Rate = value;
        Save(PlayerSettings.Rate, value);
    }

    private void OnStateChanged()
    {
        if (_player is null)
        {
            return;
        }

        State = _player.State;
        switch (State)
        {
            case PlaybackState.Playing:
                HasStartedPlaying = true;
                break;
            case PlaybackState.Paused:
                // Abrir pausado (reabertura) também passa por aqui, mas o VLC não desenha quadro
                // nenhum antes de tocar: o pôster continua até o primeiro "reproduzir".
                SaveCurrentPositionAsync().FireAndForget(_logger, "SalvarPosicao");
                break;
            case PlaybackState.Ended:
                OnEnded();
                break;
        }
    }

    private void OnPositionOrDurationChanged()
    {
        Interlocked.Exchange(ref _uiUpdateScheduled, 0);
        if (_player is null || _lesson is null)
        {
            return;
        }

        var position = _player.Position;
        var duration = _player.Duration;
        if (duration > TimeSpan.Zero && Math.Abs(DurationSeconds - duration.TotalSeconds) > 0.5)
        {
            DurationSeconds = duration.TotalSeconds;
            _progress?.SaveDurationAsync(_lesson.Id, duration).FireAndForget(_logger, "SalvarDuracao");
        }

        if (_isScrubbing || IsSettlingAfterSeek(position.TotalSeconds))
        {
            return;
        }

        SetPositionFromPlayer(position.TotalSeconds);

        if (IsPlaying && Math.Abs((position - _lastSavedPosition).TotalSeconds) >= SaveInterval.TotalSeconds)
        {
            SaveCurrentPositionAsync().FireAndForget(_logger, "SalvarPosicao");
        }

        if (!_completedThisSession && PlaybackRules.IsCompletedAt(position, duration))
        {
            MarkCompleted();
        }
    }

    private void SeekTo(double seconds)
    {
        _seekTarget = seconds;
        _seekSettleDeadline = Environment.TickCount64 + (long)SeekSettleTime.TotalMilliseconds;
        _player?.Seek(TimeSpan.FromSeconds(seconds));
    }

    private void ApplyPendingScrubSeek()
    {
        if (_pendingScrubSeek is { } target)
        {
            _pendingScrubSeek = null;
            _player?.Seek(TimeSpan.FromSeconds(target));
        }
    }

    private bool IsSettlingAfterSeek(double position)
    {
        if (_seekTarget is not { } target)
        {
            return false;
        }

        if (Math.Abs(position - target) <= SeekSettleToleranceSeconds || Environment.TickCount64 >= _seekSettleDeadline)
        {
            _seekTarget = null;
            return false;
        }

        return true;
    }

    private void OnEnded()
    {
        if (!_completedThisSession)
        {
            MarkCompleted();
        }

        if (_autoAdvance && _lesson is not null)
        {
            StartCountdownAsync(_lesson).FireAndForget(_logger, "ProximaAula");
        }
    }

    private async Task StartCountdownAsync(PlayableLesson lesson)
    {
        var order = await _progress!.GetCourseOrderAsync(lesson.CourseId);
        if (PlaybackRules.NextLesson(order, lesson.Id) is not { } next || _lesson?.Id != lesson.Id)
        {
            return;
        }

        CountdownRemaining = CountdownSeconds;
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) =>
        {
            if (CountdownRemaining is > 1)
            {
                CountdownRemaining--;
                return;
            }

            CancelCountdown();
            LoadAsync(next, autoPlay: true).FireAndForget(_logger, "ProximaAula");
        };
        _countdownTimer.Start();
    }

    private async Task GoToNeighborAsync(bool next)
    {
        if (_lesson is null || _progress is null)
        {
            return;
        }

        var order = await _progress.GetCourseOrderAsync(_lesson.CourseId);
        var target = next ? PlaybackRules.NextLesson(order, _lesson.Id) : PlaybackRules.PreviousLesson(order, _lesson.Id);
        if (target is { } id)
        {
            await LoadAsync(id, autoPlay: true);
        }
    }

    private void MarkCompleted()
    {
        if (_lesson is null || _progress is null)
        {
            return;
        }

        _completedThisSession = true;
        var lessonId = _lesson.Id;
        _logger.LogInformation("Aula concluída: {LessonId}", lessonId);
        CompleteAsync().FireAndForget(_logger, "ConcluirAula");

        async Task CompleteAsync()
        {
            await _progress.SetCompletedAsync(lessonId, true);
            LessonCompleted?.Invoke(this, lessonId);
        }
    }

    private Task SaveCurrentPositionAsync()
    {
        // Player parado (já fechou ou trocou de arquivo) informa posição zero: gravar agora
        // apagaria a posição boa salva antes (aconteceu com o fechamento chamado duas vezes).
        if (_lesson is null || _player is null || _progress is null || !HasStartedPlaying || _player.State == PlaybackState.Idle)
        {
            return Task.CompletedTask;
        }

        var position = _player.Position;
        _lastSavedPosition = position;
        return _progress.SavePositionAsync(_lesson.Id, position);
    }

    private void SetPositionFromPlayer(double seconds)
    {
        // Atualiza a barra sem que OnPositionSecondsChanged trate a mudança como seek.
        _updatingFromPlayer = true;
        try
        {
            PositionSeconds = seconds;
        }
        finally
        {
            _updatingFromPlayer = false;
        }
    }

    private async Task LoadPosterAsync(PlayableLesson lesson)
    {
        if (_thumbnails is null || _images is null)
        {
            return;
        }

        var file = new FileInfo(lesson.FilePath);
        if (!file.Exists)
        {
            return;
        }

        var request = new ThumbnailRequest(lesson.Id, lesson.FilePath, file.Length, file.LastWriteTimeUtc, lesson.Duration);
        if (_thumbnails.TryGetPath(request) is { } path)
        {
            var image = await _images.LoadAsync(path, ThumbnailImages.PosterDecodeWidth);
            if (_lesson?.Id == lesson.Id)
            {
                Poster = image;
            }

            return;
        }

        _thumbnails.ThumbnailReady += OnReady;
        _thumbnails.Request(request, ThumbnailPriority.High);

        void OnReady(object? sender, Guid id)
        {
            if (id != lesson.Id)
            {
                return;
            }

            _thumbnails.ThumbnailReady -= OnReady;
            Dispatcher.UIThread.Post(() => LoadPosterAsync(lesson).FireAndForget(_logger, "CarregarPoster"));
        }
    }

    private void Save<T>(SettingKey<T> key, T value) =>
        _settings?.SetAsync(key, value).FireAndForget(_logger, "SalvarPreferenciaDoPlayer");

    public static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : time.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
}
