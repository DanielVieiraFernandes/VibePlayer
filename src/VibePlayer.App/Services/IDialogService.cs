using Avalonia.Controls;
using VibePlayer.App.ViewModels;
using VibePlayer.App.Views.Library;
using VibePlayer.App.Views.Shell;
using VibePlayer.Core.Library;

namespace VibePlayer.App.Services;

/// <summary>
/// Janelas modais que pedem uma decisão ao usuário. Atrás de interface para os ViewModels não
/// conhecerem janelas e para os testes E2E responderem no lugar do usuário.
/// </summary>
public interface IDialogService
{
    /// <returns>A interpretação escolhida, ou <c>null</c> se o usuário cancelou.</returns>
    Task<LibraryRootKind?> AskFolderInterpretationAsync(FolderPreview preview);

    /// <returns>O destino escolhido, ou <c>null</c> se o usuário cancelou.</returns>
    Task<VideoDestination?> AskVideoDestinationAsync(IReadOnlyList<string> files);

    Task<bool> ConfirmAsync(string title, string message, string confirmLabel);
}

/// <summary><see cref="IDialogService"/> com janelas modais sobre a janela principal.</summary>
public sealed class WindowDialogService : IDialogService
{
    private Window? _owner;

    public void AttachTo(Window owner) => _owner = owner;

    public Task<LibraryRootKind?> AskFolderInterpretationAsync(FolderPreview preview) =>
        new AddLibraryFolderDialog { DataContext = new AddLibraryFolderViewModel(preview) }.ShowDialog<LibraryRootKind?>(Owner);

    public Task<VideoDestination?> AskVideoDestinationAsync(IReadOnlyList<string> files) =>
        new AddVideosDialog { DataContext = new AddVideosViewModel(files) }.ShowDialog<VideoDestination?>(Owner);

    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel) =>
        new ConfirmDialog(title, message, confirmLabel).ShowDialog<bool>(Owner);

    private Window Owner => _owner ?? throw new InvalidOperationException("Os diálogos ainda não foram presos a uma janela.");
}
