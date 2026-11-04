using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using VibePlayer.App.Resources;
using VibePlayer.App.ViewModels;

namespace VibePlayer.App.Views.Library;

public partial class LibraryView : UserControl
{
    private ContextMenu? _actionsMenu;

    public LibraryView()
    {
        InitializeComponent();
        Tree.ContextRequested += OnTreeContextRequested;
    }

    /// <summary>Menu de ações aberto agora, se houver. Exposto para os testes E2E conferirem o conteúdo.</summary>
    public ContextMenu? OpenActionsMenu => _actionsMenu is { IsOpen: true } ? _actionsMenu : null;

    /// <remarks>
    /// O menu é tratado no TreeView, e não preso ao conteúdo de cada item, por dois motivos:
    /// <list type="bullet">
    /// <item>o clique direito vale para a linha inteira do item (recuo, seta de expandir e o espaço
    /// até a borda), não só para onde há texto;</item>
    /// <item>o evento é sempre marcado como tratado: clicar numa aula sem ações não pode abrir o
    /// menu do curso em que ela está (o evento subiria até a linha do curso).</item>
    /// </list>
    /// Também atende ao teclado (tecla Menu ou Shift+F10) no item com foco.
    /// </remarks>
    private void OnTreeContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        var row = (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (row?.DataContext is LibraryItemViewModel { HasActions: true } item)
        {
            OpenActions(row, item, fromButton: false);
        }

        e.Handled = true;
    }

    private void OnActionsButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: LibraryItemViewModel item } button)
        {
            OpenActions(button, item, fromButton: true);
        }
    }

    private void OpenActions(Control target, LibraryItemViewModel item, bool fromButton)
    {
        var library = (LibraryViewModel)DataContext!;
        var menu = new ContextMenu
        {
            // Pelo botão "⋯", o menu abre logo abaixo dele; pelo clique direito, onde o ponteiro está.
            Placement = fromButton ? PlacementMode.BottomEdgeAlignedRight : PlacementMode.Pointer,
        };

        if (item.CanLocate)
        {
            menu.Items.Add(MenuItem(Strings.Library_Locate, Symbol.FolderSearch, library.LocateCommand, item));
        }

        if (item is LessonItemViewModel { IsAvailable: true } or CourseItemViewModel { IsAvailable: true })
        {
            var label = item is LessonItemViewModel ? Strings.Library_Transcribe : Strings.Library_TranscribeCourse;
            menu.Items.Add(MenuItem(label, Symbol.ClosedCaption, library.TranscribeCommand, item));
        }

        if (item is LessonItemViewModel lesson)
        {
            menu.Items.Add(lesson.IsCompleted
                ? MenuItem(Strings.Library_MarkNotCompleted, Symbol.ArrowUndo, library.ToggleCompletedCommand, item)
                : MenuItem(Strings.Library_MarkCompleted, Symbol.CheckmarkCircle, library.ToggleCompletedCommand, item));
        }

        if (item.CanRemove)
        {
            menu.Items.Add(MenuItem(Strings.Library_Remove, Symbol.Delete, library.RemoveCommand, item));
        }

        _actionsMenu?.Close();
        _actionsMenu = menu;
        menu.Open(target);
    }

    private static MenuItem MenuItem(string header, Symbol icon, System.Windows.Input.ICommand command, LibraryItemViewModel item) => new()
    {
        Header = header,
        Icon = new SymbolIcon { Symbol = icon },
        Command = command,
        CommandParameter = item,
    };
}
