using VibePlayer.Core.Library;

namespace VibePlayer.UnitTests.Core.Library;

/// <summary>Árvore de pastas em memória para testar a biblioteca sem disco.</summary>
public sealed class FakeFileSystem : IFileSystemReader
{
    private readonly Dictionary<string, List<FileSystemEntry>> _directories = new(LibraryPaths.Comparer);
    private readonly HashSet<string> _unreadable = new(LibraryPaths.Comparer);

    public FakeFileSystem()
    {
        Root = OperatingSystem.IsWindows() ? @"C:\fake" : "/fake";
        _directories[Root] = [];
    }

    public string Root { get; }

    public string PathOf(string relative) => LibraryPaths.Combine(Root, relative);

    public string AddFile(string relative, long size = 1, bool hidden = false, bool link = false)
    {
        var path = PathOf(relative);
        var parent = EnsureDirectory(Path.GetDirectoryName(path)!);
        parent.Add(new FileSystemEntry(Path.GetFileName(path), path, IsDirectory: false, size, DateTime.UnixEpoch, hidden, link));
        return path;
    }

    public string AddDirectory(string relative, bool hidden = false, bool link = false)
    {
        var path = PathOf(relative);
        EnsureDirectory(path);
        if (hidden || link)
        {
            var parent = _directories[Path.GetDirectoryName(path)!];
            var index = parent.FindIndex(e => e.FullPath == path);
            parent[index] = parent[index] with { IsHidden = hidden, IsLink = link };
        }

        return path;
    }

    public void MakeUnreadable(string relative) => _unreadable.Add(PathOf(relative));

    public bool DirectoryExists(string path) => _directories.ContainsKey(path);

    public FileSystemEntry? GetFile(string path) =>
        _directories.TryGetValue(Path.GetDirectoryName(path) ?? string.Empty, out var entries)
            ? entries.FirstOrDefault(e => !e.IsDirectory && LibraryPaths.Comparer.Equals(e.FullPath, path))
            : null;

    public IReadOnlyList<FileSystemEntry> List(string directory)
    {
        if (_unreadable.Contains(directory))
        {
            throw new UnauthorizedAccessException(directory);
        }

        return _directories.TryGetValue(directory, out var entries) ? entries : throw new DirectoryNotFoundException(directory);
    }

    private List<FileSystemEntry> EnsureDirectory(string path)
    {
        if (_directories.TryGetValue(path, out var existing))
        {
            return existing;
        }

        var entries = new List<FileSystemEntry>();
        _directories[path] = entries;
        var parent = EnsureDirectory(Path.GetDirectoryName(path)!);
        parent.Add(new FileSystemEntry(Path.GetFileName(path), path, IsDirectory: true, 0, DateTime.UnixEpoch));
        return entries;
    }
}
