using Avalonia.Controls;
using Avalonia.Interactivity;
using VibePlayer.App.ViewModels;
using VibePlayer.Core.Library;

namespace VibePlayer.App.Views.Library;

/// <summary>Devolve o destino escolhido, ou <c>null</c> ao cancelar.</summary>
public partial class AddVideosDialog : Window
{
    public AddVideosDialog()
    {
        InitializeComponent();
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e) =>
        Close((VideoDestination?)((AddVideosViewModel)DataContext!).Destination);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
