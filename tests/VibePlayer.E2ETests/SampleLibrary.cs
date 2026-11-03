using VibePlayer.App.Services;
using VibePlayer.Core.Library;

namespace VibePlayer.E2ETests;

/// <summary>
/// Pasta de cursos de exemplo gerada pelo próprio teste. Arquivos .mp4 vazios bastam: nesta fase
/// ninguém lê o conteúdo do vídeo.
/// </summary>
public sealed class SampleLibrary : IDisposable
{
    public SampleLibrary()
    {
        Root = Path.Combine(Path.GetTempPath(), "VibePlayerCursos", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string PathOf(string relative) => Path.Combine([Root, .. relative.Split('/')]);

    public string AddVideo(string relative)
    {
        var path = PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, []);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            // Pastas tornadas ilegíveis por algum teste precisam voltar ao normal para sair.
            foreach (var directory in Directory.EnumerateDirectories(Root, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
            }

            Directory.Delete(Root, recursive: true);
        }
    }
}

/// <summary>Seletor de arquivos que devolve o que o teste preparou, sem janela do sistema.</summary>
public sealed class FakeFilePicker : IFilePicker
{
    public Queue<string?> Folders { get; } = new();

    public Queue<IReadOnlyList<string>> Videos { get; } = new();

    public Task<string?> PickFolderAsync(string title) => Task.FromResult(Folders.Count > 0 ? Folders.Dequeue() : null);

    public Task<IReadOnlyList<string>> PickVideosAsync(string title) => Task.FromResult(Videos.Count > 0 ? Videos.Dequeue() : []);
}

/// <summary>Diálogos respondidos pelo teste; guarda o que foi perguntado para conferência.</summary>
public sealed class ScriptedDialogs : IDialogService
{
    public Func<FolderPreview, LibraryRootKind?> FolderAnswer { get; set; } = preview => preview.Suggested;

    public Func<IReadOnlyList<string>, VideoDestination?> VideosAnswer { get; set; } = _ => new VideoDestination.LooseVideos();

    public bool ConfirmAnswer { get; set; } = true;

    public List<FolderPreview> FolderPreviews { get; } = [];

    public List<string> Confirmations { get; } = [];

    public Task<LibraryRootKind?> AskFolderInterpretationAsync(FolderPreview preview)
    {
        FolderPreviews.Add(preview);
        return Task.FromResult(FolderAnswer(preview));
    }

    public Task<VideoDestination?> AskVideoDestinationAsync(IReadOnlyList<string> files) => Task.FromResult(VideosAnswer(files));

    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel)
    {
        Confirmations.Add(title);
        return Task.FromResult(ConfirmAnswer);
    }
}
