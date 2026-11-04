using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VibePlayer.App.Resources;
using VibePlayer.App.Services;
using VibePlayer.Core.Playback;
using VibePlayer.Core.Settings;
using VibePlayer.Core.Threading;
using VibePlayer.Core.Transcription;

namespace VibePlayer.App.ViewModels;

public enum TranscriptionViewState
{
    NoLesson,
    NotTranscribed,
    DownloadingModel,
    Queued,
    Running,
    Done,
    NoAudio,
    Failed,
    VlcMissing,
}

/// <summary>Um trecho na lista; clicar no tempo leva o vídeo àquele ponto.</summary>
public sealed record TranscriptSegmentItem(TimeSpan Start, string Text)
{
    public string Time => Start.ToString(Start.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture);

    public string JumpLabel => string.Format(CultureInfo.CurrentCulture, Strings.Transcription_JumpTo, Time);
}

/// <summary>Uma opção do seletor de idioma.</summary>
public sealed record TranscriptionLanguageOption(string Code, string Label);

/// <summary>Aba Transcrição: estado da aula aberta, progresso, texto e o pedido de transcrever.</summary>
public sealed partial class TranscriptionViewModel : ViewModelBase
{
    private readonly ITranscriptionService? _service;
    private readonly IWhisperModelStore? _models;
    private readonly ISettingsStore? _settings;
    private readonly IMediaPlayer? _player;
    private readonly IDialogService? _dialogs;
    private readonly INotificationService? _notifications;
    private readonly ILogger<TranscriptionViewModel> _logger;
    private CancellationTokenSource? _download;
    private Guid? _lessonId;

    // Só a resposta mais recente vale: trocar de aula enquanto outra carrega não pode mostrar o
    // texto da aula anterior.
    private int _refreshVersion;

    /// <summary>Usado apenas pelo previewer de XAML.</summary>
    public TranscriptionViewModel()
    {
        _logger = NullLogger<TranscriptionViewModel>.Instance;
        SelectedLanguage = Languages[0];
    }

    public TranscriptionViewModel(
        ITranscriptionService service,
        IWhisperModelStore models,
        ISettingsStore settings,
        IMediaPlayer player,
        IDialogService dialogs,
        INotificationService notifications,
        ILogger<TranscriptionViewModel> logger)
    {
        _service = service;
        _models = models;
        _settings = settings;
        _player = player;
        _dialogs = dialogs;
        _notifications = notifications;
        _logger = logger;
        SelectedLanguage = Languages[0];
        service.Changed += OnServiceChanged;
        service.ProgressChanged += OnProgressChanged;
    }

    /// <summary>O usuário clicou num tempo do texto.</summary>
    public event EventHandler<TimeSpan>? SeekRequested;

    public IReadOnlyList<TranscriptionLanguageOption> Languages { get; } =
    [
        new(TranscriptionSettings.AutomaticLanguage, Strings.Transcription_LanguageAuto),
        new("pt", Strings.Transcription_LanguagePortuguese),
        new("en", Strings.Transcription_LanguageEnglish),
        new("es", Strings.Transcription_LanguageSpanish),
    ];

    public ObservableCollection<TranscriptSegmentItem> Segments { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoLesson), nameof(IsNotTranscribed), nameof(IsWorking), nameof(CanCancel), nameof(IsDone), nameof(IsNoAudio), nameof(IsFailed), nameof(IsVlcMissing))]
    public partial TranscriptionViewState State { get; private set; }

    [ObservableProperty]
    public partial string? StageText { get; private set; }

    [ObservableProperty]
    public partial bool IsProgressKnown { get; private set; }

    /// <summary>0 a 100.</summary>
    [ObservableProperty]
    public partial double ProgressPercent { get; private set; }

    [ObservableProperty]
    public partial string? SourceText { get; private set; }

    [ObservableProperty]
    public partial bool IsFromSubtitle { get; private set; }

    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    [ObservableProperty]
    public partial TranscriptionLanguageOption SelectedLanguage { get; set; }

    public bool IsNoLesson => State == TranscriptionViewState.NoLesson;

    public bool IsNotTranscribed => State == TranscriptionViewState.NotTranscribed;

    public bool IsWorking => State is TranscriptionViewState.DownloadingModel or TranscriptionViewState.Queued or TranscriptionViewState.Running;

    public bool CanCancel => IsWorking;

    public bool IsDone => State == TranscriptionViewState.Done;

    public bool IsNoAudio => State == TranscriptionViewState.NoAudio;

    public bool IsFailed => State == TranscriptionViewState.Failed;

    public bool IsVlcMissing => State == TranscriptionViewState.VlcMissing;

    public async Task LoadPreferencesAsync()
    {
        if (_settings is not null)
        {
            var code = await _settings.GetAsync(TranscriptionSettings.Language);
            SelectedLanguage = Languages.FirstOrDefault(option => option.Code == code) ?? Languages[0];
        }
    }

    /// <summary>A aula aberta no player mudou.</summary>
    public Task ShowLessonAsync(Guid lessonId)
    {
        _lessonId = lessonId;
        _service?.SetCurrentLesson(lessonId);
        return RefreshAsync();
    }

    public Task ResumeQueueAsync() => _service?.ResumeAsync() ?? Task.CompletedTask;

    /// <summary>Aulas pedidas pelo menu da biblioteca: garante o modelo e põe na fila.</summary>
    public async Task TranscribeLessonsAsync(IReadOnlyList<Guid> lessonIds)
    {
        ArgumentNullException.ThrowIfNull(lessonIds);
        if (_service is not null && await EnsureModelAsync())
        {
            await _service.EnqueueAsync(lessonIds);
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private Task TranscribeAsync() =>
        _lessonId is { } lessonId ? TranscribeLessonsAsync([lessonId]) : Task.CompletedTask;

    [RelayCommand]
    private async Task TranscribeWithWhisperAsync()
    {
        if (_service is not null && _lessonId is { } lessonId && await EnsureModelAsync())
        {
            await _service.EnqueueWithWhisperAsync(lessonId);
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (_download is { } download)
        {
            await download.CancelAsync();
        }
        else if (_service is not null && _lessonId is { } lessonId)
        {
            await _service.CancelAsync(lessonId);
        }
    }

    [RelayCommand]
    private void Jump(TranscriptSegmentItem? segment)
    {
        if (segment is not null)
        {
            SeekRequested?.Invoke(this, segment.Start);
        }
    }

    partial void OnSelectedLanguageChanged(TranscriptionLanguageOption value)
    {
        _settings?.SetAsync(TranscriptionSettings.Language, value.Code).FireAndForget(_logger, "SalvarIdiomaDaTranscricao");
    }

    private async Task<bool> EnsureModelAsync()
    {
        if (_models is null || _settings is null || _dialogs is null || _download is not null)
        {
            return false;
        }

        var saved = WhisperModels.ByName(await _settings.GetAsync(TranscriptionSettings.Model));
        if (_models.IsAvailable(saved))
        {
            return true;
        }

        if (await _dialogs.AskWhisperModelAsync() is not { } model)
        {
            return false;
        }

        await _settings.SetAsync(TranscriptionSettings.Model, model.Name);
        return _models.IsAvailable(model) || await DownloadAsync(model);
    }

    private async Task<bool> DownloadAsync(WhisperModel model)
    {
        using var download = new CancellationTokenSource();
        _download = download;
        var previous = State;
        State = TranscriptionViewState.DownloadingModel;
        StageText = Strings.Transcription_DownloadingModel;
        IsProgressKnown = true;
        ProgressPercent = 0;
        try
        {
            // Progress<T> criado na thread de UI entrega os avisos nela.
            await _models!.DownloadAsync(model, new Progress<double>(fraction => ProgressPercent = fraction * 100), download.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (ModelDownloadException ex)
        {
            _notifications?.ShowError(Strings.Transcription_DownloadFailedTitle, DownloadErrorText(ex.Error));
            return false;
        }
        finally
        {
            _download = null;
            State = previous;
            await RefreshAsync();
        }
    }

    private void OnServiceChanged(object? sender, Guid lessonId)
    {
        if (lessonId == _lessonId)
        {
            Dispatcher.UIThread.Post(() => RefreshAsync().FireAndForget(_logger, "AtualizarTranscricao"));
        }
    }

    private void OnProgressChanged(object? sender, TranscriptionProgress progress)
    {
        if (progress.LessonId == _lessonId)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_lessonId == progress.LessonId && State == TranscriptionViewState.Running)
                {
                    StageText = StageLabel(progress.Stage);
                    IsProgressKnown = progress.Stage != TranscriptionStage.Preparing;
                    ProgressPercent = progress.Fraction * 100;
                }
            });
        }
    }

    private async Task RefreshAsync()
    {
        if (_download is not null)
        {
            return;
        }

        var version = ++_refreshVersion;
        if (_service is null || _lessonId is not { } lessonId)
        {
            State = TranscriptionViewState.NoLesson;
            return;
        }

        var result = await _service.GetAsync(lessonId);
        if (version == _refreshVersion)
        {
            Apply(result);
        }
    }

    private void Apply(LessonTranscription result)
    {
        Segments.Clear();
        SourceText = null;
        IsFromSubtitle = false;
        ErrorText = null;
        switch (result.Status)
        {
            case TranscriptionStatus.NotTranscribed:
                State = _player?.Availability == PlayerAvailability.Unavailable ? TranscriptionViewState.VlcMissing : TranscriptionViewState.NotTranscribed;
                break;
            case TranscriptionStatus.Queued:
                State = TranscriptionViewState.Queued;
                StageText = Strings.Transcription_Queued;
                IsProgressKnown = false;
                break;
            case TranscriptionStatus.Running:
                // Mantém a etapa, se os avisos de progresso já chegaram antes desta leitura.
                if (State != TranscriptionViewState.Running)
                {
                    StageText = StageLabel(TranscriptionStage.Preparing);
                    IsProgressKnown = false;
                }

                State = TranscriptionViewState.Running;
                break;
            case TranscriptionStatus.Done:
                State = TranscriptionViewState.Done;
                ShowTranscript(result.Transcript);
                break;
            case TranscriptionStatus.NoAudio:
                State = TranscriptionViewState.NoAudio;
                break;
            case TranscriptionStatus.Failed:
                ErrorText = ErrorLabel(result.ErrorCode);
                State = result.ErrorCode == TranscriptionErrors.VlcUnavailable ? TranscriptionViewState.VlcMissing : TranscriptionViewState.Failed;
                break;
        }
    }

    private void ShowTranscript(Transcript? transcript)
    {
        if (transcript is null)
        {
            return;
        }

        IsFromSubtitle = transcript.Source == TranscriptSource.Subtitle;
        SourceText = IsFromSubtitle
            ? Strings.Transcription_SourceSubtitle
            : string.Format(CultureInfo.CurrentCulture, Strings.Transcription_SourceWhisper, transcript.Model);
        foreach (var segment in transcript.Segments)
        {
            Segments.Add(new TranscriptSegmentItem(segment.Start, segment.Text));
        }
    }

    private static string StageLabel(TranscriptionStage stage) => stage switch
    {
        TranscriptionStage.Extracting => Strings.Transcription_StageExtracting,
        TranscriptionStage.Transcribing => Strings.Transcription_StageTranscribing,
        _ => Strings.Transcription_StagePreparing,
    };

    private static string ErrorLabel(string? code) => code switch
    {
        TranscriptionErrors.UnreadableFile => Strings.Transcription_ErrorUnreadable,
        TranscriptionErrors.FileMissing => Strings.Transcription_ErrorFileMissing,
        TranscriptionErrors.ModelMissing => Strings.Transcription_ErrorModelMissing,
        TranscriptionErrors.ModelInvalid => Strings.Transcription_ErrorModelInvalid,
        TranscriptionErrors.ProcessCrashed => Strings.Transcription_ErrorCrashed,
        _ => Strings.Transcription_ErrorUnexpected,
    };

    private static string DownloadErrorText(ModelDownloadError error) => error switch
    {
        ModelDownloadError.NotEnoughSpace => Strings.Transcription_DownloadNoSpace,
        ModelDownloadError.HashMismatch => Strings.Transcription_DownloadCorrupted,
        _ => Strings.Transcription_DownloadNetwork,
    };
}
