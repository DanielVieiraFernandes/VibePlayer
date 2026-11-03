using Avalonia.Controls;
using Avalonia.Interactivity;
using VibePlayer.App.ViewModels;
using VibePlayer.Core.Library;

namespace VibePlayer.App.Views.Library;

/// <summary>Devolve a interpretação escolhida, ou <c>null</c> ao cancelar.</summary>
public partial class AddLibraryFolderDialog : Window
{
    public AddLibraryFolderDialog()
    {
        InitializeComponent();
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e) =>
        Close((LibraryRootKind?)((AddLibraryFolderViewModel)DataContext!).SelectedKind);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
