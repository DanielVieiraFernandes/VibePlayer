using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using VibePlayer.App.Resources;
using VibePlayer.Core.Library;

namespace VibePlayer.App.ViewModels;

/// <summary>
/// Pergunta como entender uma pasta ("Vários cursos" ou "Um curso"), com a sugestão do app
/// pré-selecionada e a prévia do que cada opção cria. Nada é gravado até confirmar.
/// </summary>
public sealed partial class AddLibraryFolderViewModel : ViewModelBase
{
    // A prévia é para conferir, não para listar tudo: pastas com dezenas de cursos ficam legíveis.
    private const int MaxPreviewLines = 8;

    private readonly FolderPreview _preview;

    public AddLibraryFolderViewModel(FolderPreview preview)
    {
        _preview = preview;
        IsCollection = preview.Suggested == LibraryRootKind.Collection;
    }

    public string FolderPath => _preview.Path;

    public bool IsCollectionSuggested => _preview.Suggested == LibraryRootKind.Collection;

    public bool IsCourseSuggested => !IsCollectionSuggested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCourse), nameof(PreviewLines), nameof(IgnoredLooseText), nameof(HasIgnoredLoose), nameof(CanConfirm), nameof(IsEmpty))]
    public partial bool IsCollection { get; set; }

    public bool IsCourse
    {
        get => !IsCollection;
        set => IsCollection = !value;
    }

    public LibraryRootKind SelectedKind => IsCollection ? LibraryRootKind.Collection : LibraryRootKind.Course;

    public string CollectionSummary => Plural(_preview.AsCollection.Courses.Count, Strings.AddFolder_CoursesOne, Strings.AddFolder_CoursesMany);

    public string CourseSummary => _preview.AsCourse.Courses is [var course]
        ? Format(Strings.AddFolder_CourseSummary, course.ModuleCount, course.LessonCount)
        : Strings.AddFolder_NothingFound;

    public IReadOnlyList<string> PreviewLines
    {
        get
        {
            var courses = Selected.Courses;
            var lines = courses.Take(MaxPreviewLines)
                .Select(c => Format(Strings.AddFolder_CourseLine, c.Title, LessonCount(c.LessonCount)))
                .ToList();
            if (courses.Count > MaxPreviewLines)
            {
                lines.Add(Format(Strings.AddFolder_MoreCourses, courses.Count - MaxPreviewLines));
            }

            return lines;
        }
    }

    public bool HasIgnoredLoose => IsCollection && _preview.AsCollection.IgnoredLooseVideos > 0;

    public string IgnoredLooseText => Format(Strings.AddFolder_IgnoredLoose, _preview.AsCollection.IgnoredLooseVideos);

    public bool IsEmpty => Selected.Courses.Count == 0;

    public bool CanConfirm => !IsEmpty;

    private InterpretationPreview Selected => IsCollection ? _preview.AsCollection : _preview.AsCourse;

    internal static string LessonCount(int count) => Plural(count, Strings.Library_LessonCountOne, Strings.Library_LessonCountMany);

    internal static string Plural(int count, string one, string many) => count == 1 ? one : Format(many, count);

    private static string Format(string format, params object[] args) => string.Format(CultureInfo.CurrentCulture, format, args);
}
