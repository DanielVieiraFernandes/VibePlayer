using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VibePlayer.App.Resources;
using VibePlayer.App.Services;
using VibePlayer.Core;
using VibePlayer.Core.Library;
using VibePlayer.Core.Playback;
using VibePlayer.Core.Threading;
using Avalonia.Threading;

namespace VibePlayer.App.ViewModels;

/// <summary>Aula escolhida na biblioteca, mostrada pelo player.</summary>
public sealed record SelectedLesson(Guid Id, string Title, string CourseTitle);

/// <summary>
/// Aba Aulas: árvore da biblioteca e as ações de adicionar, atualizar, remover e localizar pasta.
/// </summary>
/// <remarks>
/// Erros esperados (pasta inválida, vídeo repetido) viram a faixa de erro do painel. Falhas
/// inesperadas também, depois de irem para o log: o handler global é só rede de segurança.
/// </remarks>
public sealed partial class LibraryViewModel : ViewModelBase
{
    private readonly ICourseLibrary _library;
    private readonly IFilePicker _filePicker;
    private readonly IDialogService _dialogs;
    private readonly LibraryFolderValidator _folderValidator;
    private readonly VideoSelectionValidator _videoValidator;
    private readonly IAppPaths? _paths;
    private readonly ILogger<LibraryViewModel> _logger;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ILessonProgressStore? _progress;
    private readonly ILessonDurationUpdater? _durations;
    private readonly IThumbnailService? _thumbnails;
    private readonly ThumbnailImages? _images;

    // Estado da árvore que sobrevive às recargas (ela é recriada inteira; ver LibraryItemViewModel).
    private readonly HashSet<Guid> _expanded = [];
    private int _reloadScheduled;

    /// <summary>Usado apenas pelo previewer de XAML.</summary>
    public LibraryViewModel()
    {
        _library = null!;
        _filePicker = null!;
        _dialogs = null!;
        _folderValidator = null!;
        _videoValidator = null!;
        _logger = NullLogger<LibraryViewModel>.Instance;
    }

    public LibraryViewModel(
        ICourseLibrary library,
        IFilePicker filePicker,
        IDialogService dialogs,
        LibraryFolderValidator folderValidator,
        VideoSelectionValidator videoValidator,
        IAppPaths paths,
        ILessonProgressStore progress,
        ILessonDurationUpdater durations,
        IThumbnailService thumbnails,
        ThumbnailImages images,
        ILogger<LibraryViewModel> logger)
    {
        _library = library;
        _filePicker = filePicker;
        _dialogs = dialogs;
        _folderValidator = folderValidator;
        _videoValidator = videoValidator;
        _paths = paths;
        _progress = progress;
        _durations = durations;
        _thumbnails = thumbnails;
        _images = images;
        thumbnails.ThumbnailReady += (_, lessonId) => Dispatcher.UIThread.Post(() => ApplyThumbnails(lessonId));
        _logger = logger;
    }

    public ObservableCollection<CourseItemViewModel> Courses { get; } = [];

    public bool HasCourses => Courses.Count > 0;

    [ObservableProperty]
    public partial bool IsRefreshing { get; private set; }

    [ObservableProperty]
    public partial string? ProgressText { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; private set; }

    public bool HasError => ErrorMessage is not null;

    /// <summary>Item com foco na árvore (curso, módulo ou aula).</summary>
    [ObservableProperty]
    public partial LibraryItemViewModel? SelectedItem { get; set; }

    /// <summary>
    /// Aula atual. Só muda para aula disponível: aula indisponível pode ter foco (para o menu de
    /// remover), mas não vira a aula do player.
    /// </summary>
    [ObservableProperty]
    public partial SelectedLesson? CurrentLesson { get; private set; }

    /// <summary>Carrega a biblioteca salva e, em seguida, relê as pastas em segundo plano.</summary>
    public async Task InitializeAsync()
    {
        await ReloadAsync();
        await RefreshAsync();
    }

    /// <summary>Cancela a atualização em andamento (app fechando).</summary>
    public void Shutdown() => _shutdown.Cancel();

    /// <summary>
    /// Marca na árvore a aula que o player abriu sozinho (próxima aula, reabertura do app),
    /// abrindo o curso e o módulo dela.
    /// </summary>
    public void ShowCurrentLesson(Guid lessonId)
    {
        foreach (var course in Courses)
        {
            foreach (var parent in new LibraryItemViewModel[] { course }.Concat(course.Children.OfType<ModuleItemViewModel>()))
            {
                if (parent.Children.OfType<LessonItemViewModel>().FirstOrDefault(l => l.Id == lessonId) is { } lesson)
                {
                    course.IsExpanded = true;
                    parent.IsExpanded = true;
                    SelectedItem = lesson;
                    return;
                }
            }
        }
    }

    /// <summary>Recarrega a árvore (ex.: aula concluída), agrupando pedidos seguidos numa recarga só.</summary>
    public void ReloadInBackground()
    {
        if (Interlocked.Exchange(ref _reloadScheduled, 1) == 0)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Interlocked.Exchange(ref _reloadScheduled, 0);
                ReloadAsync().FireAndForget(_logger, "RecarregarBiblioteca");
            });
        }
    }

    [RelayCommand]
    private Task AddFolderAsync() => RunAsync(async () =>
    {
        if (await _filePicker.PickFolderAsync(Strings.Library_PickFolderTitle) is not { } folder)
        {
            return;
        }

        var known = await _library.GetKnownPathsAsync(_shutdown.Token);
        if (!ShowValidation(await _folderValidator.ValidateAsync(new LibraryFolderCandidate(folder, known, AppDataDirectory), _shutdown.Token)))
        {
            return;
        }

        var preview = await _library.PreviewFolderAsync(folder, _shutdown.Token);
        if (await _dialogs.AskFolderInterpretationAsync(preview) is { } kind)
        {
            await _library.AddFolderAsync(folder, kind, _shutdown.Token);
            await ReloadAsync();
        }
    });

    [RelayCommand]
    private Task AddVideosAsync() => RunAsync(async () =>
    {
        var files = await _filePicker.PickVideosAsync(Strings.Library_PickVideosTitle);
        if (files.Count == 0)
        {
            return;
        }

        var known = await _library.GetKnownPathsAsync(_shutdown.Token);
        if (!ShowValidation(await _videoValidator.ValidateAsync(new VideoSelectionCandidate(files, known), _shutdown.Token)))
        {
            return;
        }

        if (await _dialogs.AskVideoDestinationAsync(files) is { } destination)
        {
            await _library.AddVideosAsync(files, destination, _shutdown.Token);
            await ReloadAsync();
        }
    });

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        ProgressText = string.Format(CultureInfo.CurrentCulture, Strings.Library_Refreshing, 0);
        try
        {
            // Progress<T> criado aqui, na thread de UI: os relatórios chegam de volta nela.
            var progress = new Progress<LibraryScanProgress>(p =>
                ProgressText = string.Format(CultureInfo.CurrentCulture, Strings.Library_Refreshing, p.LessonsFound));
            await _library.RefreshAsync(progress, _shutdown.Token);
            await ReloadAsync();

            // Durações em segundo plano; a árvore recarrega a cada lote gravado.
            _durations?.UpdateMissingAsync(ReloadInBackground, _shutdown.Token).FireAndForget(_logger, "LerDuracoes");
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // App fechando: nada a mostrar.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao atualizar a biblioteca");
            ErrorMessage = Strings.Library_RefreshFailed;
        }
        finally
        {
            IsRefreshing = false;
            ProgressText = null;
        }
    }

    [RelayCommand]
    private Task RemoveAsync(LibraryItemViewModel? item) => RunAsync(async () =>
    {
        if (item is not { CanRemove: true })
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            Strings.Library_RemoveConfirmTitle,
            string.Format(CultureInfo.CurrentCulture, Strings.Library_RemoveConfirmMessage, item.Title),
            Strings.Common_Remove);
        if (!confirmed)
        {
            return;
        }

        if (item is CourseItemViewModel course)
        {
            await _library.RemoveCourseAsync(course.Node.Id, _shutdown.Token);
        }
        else if (item is LessonItemViewModel lesson)
        {
            await _library.RemoveLessonAsync(lesson.Id, _shutdown.Token);
        }

        await ReloadAsync();
    });

    [RelayCommand]
    private Task LocateAsync(LibraryItemViewModel? item) => RunAsync(async () =>
    {
        if (item is not CourseItemViewModel { CanLocate: true, Node: { RootId: { } rootId, RootPath: { } rootPath } } course)
        {
            return;
        }

        if (await _filePicker.PickFolderAsync(Strings.Library_PickLocateTitle) is not { } folder)
        {
            return;
        }

        var known = await _library.GetKnownPathsAsync(_shutdown.Token);
        var candidate = new LibraryFolderCandidate(folder, known, AppDataDirectory, ReplacingRootPath: rootPath);
        if (!ShowValidation(await _folderValidator.ValidateAsync(candidate, _shutdown.Token)))
        {
            return;
        }

        if (await _library.CountKnownLessonsAtAsync(rootId, folder, _shutdown.Token) == 0)
        {
            var useAnyway = await _dialogs.ConfirmAsync(
                Strings.Library_LocateNoMatchTitle,
                string.Format(CultureInfo.CurrentCulture, Strings.Library_LocateNoMatchMessage, course.Title),
                Strings.Library_UseAnyway);
            if (!useAnyway)
            {
                return;
            }
        }

        await _library.RelocateRootAsync(rootId, folder, _shutdown.Token);
        await ReloadAsync();
    });

    [RelayCommand]
    private void DismissError() => ErrorMessage = null;

    [RelayCommand]
    private Task ToggleCompletedAsync(LibraryItemViewModel? item) => RunAsync(async () =>
    {
        if (item is LessonItemViewModel lesson && _progress is not null)
        {
            await _progress.SetCompletedAsync(lesson.Id, !lesson.IsCompleted, _shutdown.Token);
            await ReloadAsync();
        }
    });

    partial void OnSelectedItemChanged(LibraryItemViewModel? value)
    {
        if (value is LessonItemViewModel { IsAvailable: true } lesson)
        {
            CurrentLesson = new SelectedLesson(lesson.Id, lesson.Title, lesson.CourseTitle);
        }
    }

    private async Task ReloadAsync()
    {
        var selectedLessonId = (SelectedItem as LessonItemViewModel)?.Id;
        var courses = await _library.GetCoursesAsync(_shutdown.Token);
        Courses.Clear();
        foreach (var node in courses)
        {
            var course = new CourseItemViewModel(node);
            TrackExpansion(course, node.Id);
            foreach (var module in course.Children.OfType<ModuleItemViewModel>())
            {
                TrackExpansion(module, module.Id);
            }

            Courses.Add(course);
            RequestCover(course);
        }

        OnPropertyChanged(nameof(HasCourses));
        if (selectedLessonId is { } id)
        {
            ShowCurrentLesson(id);
        }
    }

    /// <summary>
    /// Mantém aberto o que estava aberto e, ao abrir um curso ou módulo, pede as miniaturas das
    /// aulas dele (as de cursos fechados só são geradas quando alguém os abre; spec 0006).
    /// </summary>
    private void TrackExpansion(LibraryItemViewModel item, Guid id)
    {
        item.IsExpanded = _expanded.Contains(id);
        if (item.IsExpanded)
        {
            RequestLessonThumbnails(item);
        }

        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(LibraryItemViewModel.IsExpanded))
            {
                return;
            }

            if (item.IsExpanded)
            {
                _expanded.Add(id);
                RequestLessonThumbnails(item);
            }
            else
            {
                _expanded.Remove(id);
            }
        };
    }

    private void RequestLessonThumbnails(LibraryItemViewModel parent)
    {
        foreach (var lesson in parent.Children.OfType<LessonItemViewModel>())
        {
            ShowThumbnail(lesson, lesson.Node, ThumbnailPriority.Normal);
        }
    }

    // A capa do curso é a miniatura da primeira aula disponível.
    private void RequestCover(CourseItemViewModel course)
    {
        if (course.AllLessons.FirstOrDefault(l => l.IsAvailable) is { } first)
        {
            ShowThumbnail(course, first.Node, ThumbnailPriority.Low);
        }
    }

    private void ShowThumbnail(LibraryItemViewModel item, LessonNode lesson, ThumbnailPriority priority)
    {
        if (_thumbnails is null || _images is null || lesson.Thumbnail is not { } request || !lesson.IsAvailable)
        {
            return;
        }

        if (_thumbnails.TryGetPath(request) is { } path)
        {
            LoadThumbnailAsync(item, path).FireAndForget(_logger, "CarregarMiniatura");
        }
        else
        {
            _thumbnails.Request(request, priority);
        }
    }

    private async Task LoadThumbnailAsync(LibraryItemViewModel item, string path) =>
        item.Thumbnail = await _images!.LoadAsync(path);

    private void ApplyThumbnails(Guid lessonId)
    {
        foreach (var course in Courses)
        {
            var lessons = course.AllLessons.Where(l => l.Id == lessonId).ToList();
            foreach (var lesson in lessons)
            {
                ShowThumbnail(lesson, lesson.Node, ThumbnailPriority.Normal);
            }

            if (course.AllLessons.FirstOrDefault(l => l.IsAvailable)?.Id == lessonId)
            {
                RequestCover(course);
            }
        }
    }

    private async Task RunAsync(Func<Task> operation)
    {
        ErrorMessage = null;
        try
        {
            await operation();
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha numa operação da biblioteca");
            ErrorMessage = Strings.Library_OperationFailed;
        }
    }

    /// <returns>Se passou; senão, a faixa de erro mostra a primeira falha.</returns>
    private bool ShowValidation(ValidationResult result)
    {
        if (result.IsValid)
        {
            return true;
        }

        var error = result.Errors[0];
        ErrorMessage = string.Format(CultureInfo.CurrentCulture, ErrorText(error.ErrorCode), error.CustomState);
        return false;
    }

    private static string ErrorText(string code) => code switch
    {
        LibraryErrorCodes.FolderNotFound => Strings.Library_Error_FolderNotFound,
        LibraryErrorCodes.AppDataFolder => Strings.Library_Error_AppDataFolder,
        LibraryErrorCodes.AlreadyAdded => Strings.Library_Error_AlreadyAdded,
        LibraryErrorCodes.OverlapsExisting => Strings.Library_Error_OverlapsExisting,
        LibraryErrorCodes.NoVideos => Strings.Library_Error_NoVideos,
        LibraryErrorCodes.NoFiles => Strings.Library_Error_NoFiles,
        LibraryErrorCodes.FileNotFound => Strings.Library_Error_FileNotFound,
        LibraryErrorCodes.NotAVideo => Strings.Library_Error_NotAVideo,
        LibraryErrorCodes.VideoAlreadyAdded => Strings.Library_Error_VideoAlreadyAdded,
        _ => Strings.Library_OperationFailed,
    };

    private string AppDataDirectory => _paths?.DataDirectory ?? string.Empty;
}
