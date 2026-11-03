namespace VibePlayer.Core.Library;

/// <summary>Uma entrada de pasta, como o scanner da biblioteca precisa vê-la.</summary>
/// <param name="IsHidden">Oculto ou de sistema segundo o sistema de arquivos.</param>
/// <param name="IsLink">Link simbólico, junção ou atalho de pasta: nunca é seguido.</param>
public sealed record FileSystemEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    long Size,
    DateTime LastWriteUtc,
    bool IsHidden = false,
    bool IsLink = false);

/// <summary>
/// Acesso de leitura ao sistema de arquivos, atrás de interface para a lógica da biblioteca
/// (ordem, filtros, detecção) ser testada com árvores falsas, sem disco.
/// </summary>
public interface IFileSystemReader
{
    bool DirectoryExists(string path);

    /// <summary>Arquivo existente, ou <c>null</c>.</summary>
    FileSystemEntry? GetFile(string path);

    /// <summary>Conteúdo direto de uma pasta.</summary>
    /// <exception cref="UnauthorizedAccessException">Sem permissão de leitura.</exception>
    /// <exception cref="IOException">Pasta inacessível (disco removido, rede caiu).</exception>
    IReadOnlyList<FileSystemEntry> List(string directory);
}
