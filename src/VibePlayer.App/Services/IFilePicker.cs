using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VibePlayer.App.Resources;
using VibePlayer.Core.Library;

namespace VibePlayer.App.Services;

/// <summary>Seletores de pasta e de arquivos do sistema, atrás de interface para os testes E2E.</summary>
public interface IFilePicker
{
    /// <returns>Caminho local da pasta, ou <c>null</c> se o usuário cancelou.</returns>
    Task<string?> PickFolderAsync(string title);

    /// <returns>Caminhos locais dos vídeos; vazio se o usuário cancelou.</returns>
    Task<IReadOnlyList<string>> PickVideosAsync(string title);
}

/// <summary><see cref="IFilePicker"/> sobre o StorageProvider do Avalonia, preso à janela principal.</summary>
public sealed class StorageFilePicker : IFilePicker
{
    private TopLevel? _host;

    public void AttachTo(TopLevel host) => _host = host;

    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await Storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });

        // TryGetLocalPath: o seletor pode devolver itens sem caminho local (ex.: provedores de
        // nuvem no macOS); esses não servem para a biblioteca, que lê o disco.
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task<IReadOnlyList<string>> PickVideosAsync(string title)
    {
        var files = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType(Strings.Library_VideoFilesFilter)
                {
                    Patterns = VideoFormats.Extensions.Select(extension => "*" + extension).ToList(),
                },
            ],
        });

        return files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
    }

    private IStorageProvider Storage =>
        _host?.StorageProvider ?? throw new InvalidOperationException("O seletor de arquivos ainda não foi preso a uma janela.");
}
