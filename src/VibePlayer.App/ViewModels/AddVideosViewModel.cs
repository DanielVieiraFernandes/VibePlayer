using CommunityToolkit.Mvvm.ComponentModel;
using VibePlayer.App.Resources;
using VibePlayer.Core.Library;

namespace VibePlayer.App.ViewModels;

/// <summary>Pergunta para onde vão vídeos avulsos: um curso novo ou o grupo "Vídeos avulsos".</summary>
public sealed partial class AddVideosViewModel : ViewModelBase
{
    public AddVideosViewModel(IReadOnlyList<string> files)
    {
        Files = files;
        IsNewCourse = true;
        CourseName = SuggestName(files);
    }

    public IReadOnlyList<string> Files { get; }

    public string Summary => AddLibraryFolderViewModel.Plural(Files.Count, Strings.AddVideos_SummaryOne, Strings.AddVideos_SummaryMany);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLooseVideos), nameof(CanConfirm))]
    public partial bool IsNewCourse { get; set; }

    public bool IsLooseVideos
    {
        get => !IsNewCourse;
        set => IsNewCourse = !value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    public partial string CourseName { get; set; }

    public bool CanConfirm => IsLooseVideos || !string.IsNullOrWhiteSpace(CourseName);

    public VideoDestination Destination => IsNewCourse
        ? new VideoDestination.NewCourse(CourseName.Trim())
        : new VideoDestination.LooseVideos();

    /// <summary>A pasta em comum dos vídeos; sem pasta em comum, o nome do primeiro vídeo.</summary>
    internal static string SuggestName(IReadOnlyList<string> files)
    {
        var folders = files.Select(Path.GetDirectoryName).Distinct(LibraryPaths.Comparer).ToList();
        return folders is [{ } folder] && Path.GetFileName(folder) is { Length: > 0 } name
            ? name
            : Path.GetFileNameWithoutExtension(files.OrderBy(f => f, NaturalStringComparer.Instance).FirstOrDefault() ?? string.Empty);
    }
}
