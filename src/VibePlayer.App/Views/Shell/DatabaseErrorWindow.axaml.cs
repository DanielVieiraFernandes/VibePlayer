using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VibePlayer.App.Resources;

namespace VibePlayer.App.Views.Shell;

public partial class DatabaseErrorWindow : Window
{
    /// <summary>Usado apenas pelo previewer de XAML.</summary>
    public DatabaseErrorWindow()
        : this(backupPath: null, logsDirectory: string.Empty)
    {
    }

    /// <param name="backupPath">Backup restaurado, quando houve um; o usuário pode precisar dele.</param>
    public DatabaseErrorWindow(string? backupPath, string logsDirectory)
    {
        InitializeComponent();

        // Selecionável para o usuário poder copiar o caminho.
        MessageText.Text = backupPath is not null
            ? string.Format(CultureInfo.CurrentCulture, Strings.DatabaseError_BackupMessage, backupPath)
            : string.Format(CultureInfo.CurrentCulture, Strings.DatabaseError_NoBackupMessage, logsDirectory);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
