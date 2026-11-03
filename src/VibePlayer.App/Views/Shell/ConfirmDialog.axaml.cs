using Avalonia.Controls;
using Avalonia.Interactivity;

namespace VibePlayer.App.Views.Shell;

/// <summary>Devolve <c>true</c> ao confirmar e <c>false</c> ao cancelar ou fechar.</summary>
public partial class ConfirmDialog : Window
{
    /// <summary>Usado apenas pelo previewer de XAML.</summary>
    public ConfirmDialog()
        : this(string.Empty, string.Empty, string.Empty)
    {
    }

    public ConfirmDialog(string title, string message, string confirmLabel)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmLabel;
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
