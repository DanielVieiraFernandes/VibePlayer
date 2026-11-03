using System.Globalization;
using VibePlayer.App.Resources;
using VibePlayer.Core.Library;

namespace VibePlayer.App.ViewModels;

/// <summary>Item da árvore da biblioteca (curso, módulo ou aula).</summary>
/// <remarks>
/// Imutável: a árvore é recriada a cada recarga, que é rara (adicionar, remover, atualizar) e
/// barata; atualizar itens no lugar exigiria casar nós e não compensa nesta escala.
/// </remarks>
public abstract class LibraryItemViewModel : ViewModelBase
{
    public abstract string Title { get; }

    /// <summary>Segunda linha (contagem de aulas, "Indisponível"); nula quando não há.</summary>
    public virtual string? Caption => null;

    public abstract bool IsAvailable { get; }

    public virtual IReadOnlyList<LibraryItemViewModel> Children => [];

    public virtual bool CanRemove => false;

    public virtual bool CanLocate => false;

    public bool HasActions => CanRemove || CanLocate;

    /// <summary>Nome lido pelos leitores de tela: título e, se houver, a segunda linha.</summary>
    public string AccessibleName => Caption is null ? Title : $"{Title}, {Caption}";
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
        _ => AddLibraryFolderViewModel.LessonCount(Node.LessonCount),
    };

    public override bool IsAvailable => Node.IsAvailable;

    public override IReadOnlyList<LibraryItemViewModel> Children { get; }

    public override bool CanRemove => true;

    /// <summary>"Localizar pasta" só faz sentido para pasta adicionada que sumiu inteira.</summary>
    public override bool CanLocate => Node is { Kind: CourseKind.Folder, IsRootAvailable: false, RootId: not null };
}

public sealed class ModuleItemViewModel(ModuleNode node, string courseTitle) : LibraryItemViewModel
{
    public override string Title => node.Title;

    public override bool IsAvailable => node.IsAvailable;

    public override IReadOnlyList<LibraryItemViewModel> Children { get; } =
        node.Lessons.Select(l => new LessonItemViewModel(l, courseTitle, canRemove: false)).ToList();
}

public sealed class LessonItemViewModel(LessonNode node, string courseTitle, bool canRemove) : LibraryItemViewModel
{
    public Guid Id => node.Id;

    public string CourseTitle => courseTitle;

    public override string Title => node.Title;

    public override string? Caption => node.IsAvailable ? null : Strings.Library_LessonUnavailable;

    public override bool IsAvailable => node.IsAvailable;

    public override bool CanRemove => canRemove;
}
