using FluentValidation;

namespace VibePlayer.Core.Library;

/// <summary>Códigos de erro dos validadores; a UI traduz cada um para um texto do Strings.resx.</summary>
/// <remarks>
/// Códigos, e não mensagens, porque o Core não conhece os textos da interface (que ficam no App,
/// em pt-BR e prontos para outros idiomas).
/// </remarks>
public static class LibraryErrorCodes
{
    public const string FolderNotFound = "Library.FolderNotFound";
    public const string AppDataFolder = "Library.AppDataFolder";
    public const string AlreadyAdded = "Library.AlreadyAdded";
    public const string OverlapsExisting = "Library.OverlapsExisting";
    public const string NoVideos = "Library.NoVideos";
    public const string NoFiles = "Library.NoFiles";
    public const string FileNotFound = "Library.FileNotFound";
    public const string NotAVideo = "Library.NotAVideo";
    public const string VideoAlreadyAdded = "Library.VideoAlreadyAdded";
}

/// <param name="ReplacingRootPath">Em "Localizar pasta": a raiz que está sendo trocada, ignorada nas regras de sobreposição.</param>
public sealed record LibraryFolderCandidate(string Path, LibraryKnownPaths Known, string AppDataDirectory, string? ReplacingRootPath = null);

/// <summary>Pasta que o usuário quer adicionar (ou usar em "Localizar pasta").</summary>
public sealed class LibraryFolderValidator : AbstractValidator<LibraryFolderCandidate>
{
    public LibraryFolderValidator(IFileSystemReader fileSystem)
    {
        var scanner = new LibraryScanner(fileSystem);

        // Para na primeira falha: não faz sentido procurar vídeos numa pasta que não existe.
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Path)
            .Must(fileSystem.DirectoryExists).WithErrorCode(LibraryErrorCodes.FolderNotFound);

        RuleFor(c => c)
            .Must(c => !LibraryPaths.IsSameOrInside(c.Path, c.AppDataDirectory) && !LibraryPaths.IsSameOrInside(c.AppDataDirectory, c.Path))
            .WithErrorCode(LibraryErrorCodes.AppDataFolder)
            .Must(c => !OtherRoots(c).Any(root => LibraryPaths.NormalizeFull(root).Equals(LibraryPaths.NormalizeFull(c.Path), LibraryPaths.Comparison)))
            .WithErrorCode(LibraryErrorCodes.AlreadyAdded)
            .Must(c => !OtherRoots(c).Any(root => LibraryPaths.IsSameOrInside(c.Path, root) || LibraryPaths.IsSameOrInside(root, c.Path)))
            .WithErrorCode(LibraryErrorCodes.OverlapsExisting)
            .Must(c => HasVideos(scanner, c.Path))
            .WithErrorCode(LibraryErrorCodes.NoVideos);
    }

    private static IEnumerable<string> OtherRoots(LibraryFolderCandidate candidate) =>
        candidate.Known.RootPaths.Where(root => candidate.ReplacingRootPath is null
            || !LibraryPaths.NormalizeFull(root).Equals(LibraryPaths.NormalizeFull(candidate.ReplacingRootPath), LibraryPaths.Comparison));

    private static bool HasVideos(LibraryScanner scanner, string path)
    {
        try
        {
            return scanner.ScanRoot(path, LibraryRootKind.Course).Courses.Count > 0;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}

public sealed record VideoSelectionCandidate(IReadOnlyList<string> Files, LibraryKnownPaths Known);

/// <summary>Vídeos avulsos que o usuário quer adicionar.</summary>
/// <remarks>O nome do arquivo com problema vai no <c>CustomState</c> do erro, para a mensagem citá-lo.</remarks>
public sealed class VideoSelectionValidator : AbstractValidator<VideoSelectionCandidate>
{
    public VideoSelectionValidator(IFileSystemReader fileSystem)
    {
        RuleFor(c => c.Files).NotEmpty().WithErrorCode(LibraryErrorCodes.NoFiles);

        RuleForEach(c => c.Files)
            .Cascade(CascadeMode.Stop)
            .Must(file => fileSystem.GetFile(file) is not null)
            .WithErrorCode(LibraryErrorCodes.FileNotFound)
            .WithState((_, file) => Path.GetFileName(file))
            .Must(VideoFormats.IsVideo)
            .WithErrorCode(LibraryErrorCodes.NotAVideo)
            .WithState((_, file) => Path.GetFileName(file))
            .Must((c, file) => !c.Known.VideoFiles.Any(known => LibraryPaths.NormalizeFull(known).Equals(LibraryPaths.NormalizeFull(file), LibraryPaths.Comparison))
                && !c.Known.RootPaths.Any(root => LibraryPaths.IsSameOrInside(file, root)))
            .WithErrorCode(LibraryErrorCodes.VideoAlreadyAdded)
            .WithState((_, file) => Path.GetFileName(file));
    }
}
