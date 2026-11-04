using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using VibePlayer.App.Resources;
using VibePlayer.Core.Library;

namespace VibePlayer.App.ViewModels;

/// <summary>Item da árvore da biblioteca (curso, módulo ou aula).</summary>
/// <remarks>
/// O conteúdo é imutável: a árvore é recriada a cada recarga, que é rara (adicionar, remover,
/// atualizar, lote de durações) e barata; atualizar itens no lugar exigiria casar nós e não
/// compensa nesta escala. Só o que muda sem recarga é observável: expandido e miniatura.
/// </remarks>
public abstract partial class LibraryItemViewModel : ViewModelBase
{
    public abstract string Title { get; }

    /// <summary>Segunda linha (contagem de aulas, duração, "Indisponível"); nula quando não há.</summary>
    public virtual string? Caption => null;

    public abstract bool IsAvailable { get; }

    public virtual IReadOnlyList<LibraryItemViewModel> Children => [];

    public virtual bool CanRemove => false;

    public virtual bool CanLocate => false;

    /// <summary>Marcar ou desmarcar como concluída (só aulas).</summary>
    public virtual bool CanToggleCompleted => false;

    public bool HasActions => CanRemove || CanLocate || CanToggleCompleted;

    /// <summary>Nome do botão "⋯" (leitores de tela e dica): "Ações de “Curso X”".</summary>
    public string ActionsName => string.Format(CultureInfo.CurrentCulture, Strings.Library_ItemActionsFor, Title);

    /// <summary>Nome lido pelos leitores de tela: título e, se houver, a segunda linha.</summary>
    public string AccessibleName => Caption is null ? Title : $"{Title}, {Caption}";

    /// <summary>Aberto na árvore. A biblioteca guarda quais estão abertos para manter entre recargas.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Miniatura da aula ou capa do curso; nula enquanto não existe (aparece o ícone de vídeo).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    public partial Bitmap? Thumbnail { get; set; }

    public bool HasThumbnail => Thumbnail is not null;

    /// <summary>Cursos e aulas têm miniatura; módulos não.</summary>
    public virtual bool ShowsThumbnail => false;

    internal static string FormatDuration(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? string.Format(CultureInfo.CurrentCulture, "{0} h {1:00} min", (int)duration.TotalHours, duration.Minutes)
            : duration.TotalMinutes >= 1
                ? string.Format(CultureInfo.CurrentCulture, "{0} min", (int)Math.Round(duration.TotalMinutes))
                : string.Format(CultureInfo.CurrentCulture, "{0} s", (int)duration.TotalSeconds);
}

public sealed class CourseItemViewModel : LibraryItemViewModel
{
    public CourseItemViewModel(CourseNode node)
    {
        Node = node;
        var displayTitle = node.IsLooseVideos ? Strings.Library_LooseVideos : node.Title;
        Children =
        [
            .. node.Lessons.Select(l => new LessonItemViewModel(l, displayTitle, canRemove: node.Kind == CourseKind.Files)),
            .. node.Modules.Select(m => new ModuleItemViewModel(m, displayTitle)),
        ];
    }

    public CourseNode Node { get; }

    public override string Title => Node.IsLooseVideos ? Strings.Library_LooseVideos : Node.Title;

    public override string Caption => Node switch
    {
        { IsRootAvailable: false } => string.Format(CultureInfo.CurrentCulture, Strings.Library_FolderUnavailable, Node.RootPath),
        { IsAvailable: false, Kind: CourseKind.Files } => Strings.Library_VideosUnavailable,
        { IsAvailable: false } => string.Format(CultureInfo.CurrentCulture, Strings.Library_FolderUnavailable, Node.RootPath),
        _ => ProgressCaption(),
    };

    public override bool IsAvailable => Node.IsAvailable;

    public override IReadOnlyList<LibraryItemViewModel> Children { get; }

    public override bool CanRemove => true;

    /// <summary>"Localizar pasta" só faz sentido para pasta adicionada que sumiu inteira.</summary>
    public override bool CanLocate => Node is { Kind: CourseKind.Folder, IsRootAvailable: false, RootId: not null };

    public override bool ShowsThumbnail => true;

    /// <summary>Todas as aulas do curso, atravessando módulos, na ordem da árvore.</summary>
    public IEnumerable<LessonItemViewModel> AllLessons =>
        Children.SelectMany(c => c is LessonItemViewModel lesson ? [lesson] : c.Children.OfType<LessonItemViewModel>());

    // "8 de 20 aulas · 3 h 40 min"; sem nada concluído, só "20 aulas · …".
    private string ProgressCaption()
    {
        var count = Node.CompletedCount > 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.Library_CourseProgress, Node.CompletedCount, Node.LessonCount)
            : AddLibraryFolderViewModel.LessonCount(Node.LessonCount);
        return Node.TotalDuration > TimeSpan.Zero ? $"{count} · {FormatDuration(Node.TotalDuration)}" : count;
    }
}

public sealed class ModuleItemViewModel(ModuleNode node, string courseTitle) : LibraryItemViewModel
{
    public Guid Id => node.Id;

    public override string Title => node.Title;

    public override bool IsAvailable => node.IsAvailable;

    public override IReadOnlyList<LibraryItemViewModel> Children { get; } =
        node.Lessons.Select(l => new LessonItemViewModel(l, courseTitle, canRemove: false)).ToList();
}

public sealed class LessonItemViewModel(LessonNode node, string courseTitle, bool canRemove) : LibraryItemViewModel
{
    public Guid Id => node.Id;

    public LessonNode Node => node;

    public string CourseTitle => courseTitle;

    public override string Title => node.Title;

    public override string? Caption
    {
        get
        {
            if (!node.IsAvailable)
            {
                return Strings.Library_LessonUnavailable;
            }

            var parts = new List<string>();
            if (node.Duration is { } duration)
            {
                parts.Add(duration.ToString(duration.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture));
            }

            switch (node.Status)
            {
                case LessonStatus.Completed:
                    parts.Add(Strings.Library_LessonCompleted);
                    break;
                case LessonStatus.InProgress:
                    parts.Add(Strings.Library_LessonInProgress);
                    break;
            }

            return parts.Count > 0 ? string.Join(" · ", parts) : null;
        }
    }

    public override bool IsAvailable => node.IsAvailable;

    public override bool CanRemove => canRemove;

    public override bool CanToggleCompleted => true;

    public override bool ShowsThumbnail => true;

    public bool IsCompleted => node.Status == LessonStatus.Completed;

    public bool IsInProgress => node.Status == LessonStatus.InProgress;
}

/// <summary>Ícones de progresso da árvore: só aulas têm, e o template é um só para os três níveis.</summary>
public static class LibraryItemConverters
{
    public static readonly IValueConverter IsCompleted =
        new FuncValueConverter<LibraryItemViewModel?, bool>(item => item is LessonItemViewModel { IsCompleted: true });

    public static readonly IValueConverter IsInProgress =
        new FuncValueConverter<LibraryItemViewModel?, bool>(item => item is LessonItemViewModel { IsInProgress: true });
}
