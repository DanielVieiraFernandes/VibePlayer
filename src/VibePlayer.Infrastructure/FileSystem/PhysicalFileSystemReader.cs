using System.Text;
using VibePlayer.Core.Library;

namespace VibePlayer.Infrastructure.FileSystem;

/// <summary><see cref="IFileSystemReader"/> sobre o disco de verdade.</summary>
public sealed class PhysicalFileSystemReader : IFileSystemReader
{
    // IgnoreInaccessible = false: uma pasta sem permissão precisa lançar para o scanner contá-la
    // como pulada; ignorar em silêncio esconderia aulas sem avisar ninguém.
    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = false,
        AttributesToSkip = 0,
        ReturnSpecialDirectories = false,
    };

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public FileSystemEntry? GetFile(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? ToEntry(info) : null;
    }

    public IReadOnlyList<FileSystemEntry> List(string directory) =>
        new DirectoryInfo(directory).EnumerateFileSystemInfos("*", Options).Select(ToEntry).ToList();

    private static FileSystemEntry ToEntry(FileSystemInfo info)
    {
        var isDirectory = info is DirectoryInfo;
        return new FileSystemEntry(
            // NFC: o macOS devolve nomes em NFD (ver LibraryPaths).
            info.Name.Normalize(NormalizationForm.FormC),
            info.FullName,
            isDirectory,
            info is FileInfo file ? file.Length : 0,
            info.LastWriteTimeUtc,
            IsHidden: (info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0,
            // ReparsePoint cobre links simbólicos e junções no Windows; LinkTarget, os do Unix.
            IsLink: (info.Attributes & FileAttributes.ReparsePoint) != 0 || info.LinkTarget is not null);
    }
}
