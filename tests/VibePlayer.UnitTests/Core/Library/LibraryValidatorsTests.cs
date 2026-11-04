using VibePlayer.Core.Library;

namespace VibePlayer.UnitTests.Core.Library;

public sealed class LibraryFolderValidatorTests
{
    private readonly FakeFileSystem _fs = new();
    private readonly LibraryFolderValidator _validator;
    private readonly string _appData;

    public LibraryFolderValidatorTests()
    {
        _validator = new LibraryFolderValidator(_fs);
        _appData = _fs.AddDirectory("Dados/VibePlayer");
        _fs.AddFile("Cursos/Curso A/a.mp4");
        _fs.AddFile("Outros/b.mp4");
        _fs.AddFile("Dados/VibePlayer/backups/x.mp4");
        _fs.AddDirectory("Vazia");
    }

    private string? Error(string relative, params string[] knownRoots) =>
        _validator.Validate(new LibraryFolderCandidate(_fs.PathOf(relative), new LibraryKnownPaths(knownRoots.Select(_fs.PathOf).ToList(), []), _appData))
            .Errors.FirstOrDefault()?.ErrorCode;

    [Fact]
    public void PastaValida_Passa() => Error("Outros", "Cursos").ShouldBeNull();

    [Fact]
    public void PastaInexistente_Recusa() => Error("Não existe").ShouldBe(LibraryErrorCodes.FolderNotFound);

    [Fact]
    public void PastaJaAdicionada_Recusa() => Error("Cursos", "Cursos").ShouldBe(LibraryErrorCodes.AlreadyAdded);

    [Fact]
    public void PastaDentroDeOutraJaAdicionada_Recusa() => Error("Cursos/Curso A", "Cursos").ShouldBe(LibraryErrorCodes.OverlapsExisting);

    [Fact]
    public void PastaAcimaDeOutraJaAdicionada_Recusa() => Error("Cursos", "Cursos/Curso A").ShouldBe(LibraryErrorCodes.OverlapsExisting);

    [Fact]
    public void PastaDeDadosDoApp_Recusa()
    {
        Error("Dados/VibePlayer").ShouldBe(LibraryErrorCodes.AppDataFolder);
        Error("Dados").ShouldBe(LibraryErrorCodes.AppDataFolder);
    }

    [Fact]
    public void PastaSemVideos_Recusa() => Error("Vazia").ShouldBe(LibraryErrorCodes.NoVideos);

    [Fact]
    public void LocalizarPasta_IgnoraARaizQueEstaSendoTrocada()
    {
        var known = new LibraryKnownPaths([_fs.PathOf("Cursos")], []);

        var result = _validator.Validate(new LibraryFolderCandidate(_fs.PathOf("Cursos/Curso A"), known, _appData, ReplacingRootPath: _fs.PathOf("Cursos")));

        result.IsValid.ShouldBeTrue();
    }
}

public sealed class VideoSelectionValidatorTests
{
    private readonly FakeFileSystem _fs = new();
    private readonly VideoSelectionValidator _validator;

    public VideoSelectionValidatorTests()
    {
        _validator = new VideoSelectionValidator(_fs);
    }

    [Fact]
    public void VideosNovos_Passam()
    {
        var files = new[] { _fs.AddFile("Downloads/a.mp4"), _fs.AddFile("Downloads/b.mkv") };

        _validator.Validate(new VideoSelectionCandidate(files, new LibraryKnownPaths([], []))).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void VideoJaNaBiblioteca_RecusaCitandoOArquivo()
    {
        var file = _fs.AddFile("Downloads/a.mp4");

        var error = _validator.Validate(new VideoSelectionCandidate([file], new LibraryKnownPaths([], [file]))).Errors.ShouldHaveSingleItem();

        error.ErrorCode.ShouldBe(LibraryErrorCodes.VideoAlreadyAdded);
        error.CustomState.ShouldBe("a.mp4");
    }

    [Fact]
    public void VideoDentroDeUmaPastaDaBiblioteca_Recusa()
    {
        var file = _fs.AddFile("Cursos/Curso/a.mp4");

        var error = _validator.Validate(new VideoSelectionCandidate([file], new LibraryKnownPaths([_fs.PathOf("Cursos")], []))).Errors.ShouldHaveSingleItem();

        error.ErrorCode.ShouldBe(LibraryErrorCodes.VideoAlreadyAdded);
    }

    [Fact]
    public void ArquivoQueNaoEhVideo_Recusa()
    {
        var pdf = _fs.AddFile("Downloads/apostila.pdf");

        _validator.Validate(new VideoSelectionCandidate([pdf], new LibraryKnownPaths([], [])))
            .Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(LibraryErrorCodes.NotAVideo);
    }

    [Fact]
    public void ArquivoInexistente_Recusa()
    {
        var missing = _fs.PathOf("Downloads/sumiu.mp4");

        _validator.Validate(new VideoSelectionCandidate([missing], new LibraryKnownPaths([], [])))
            .Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(LibraryErrorCodes.FileNotFound);
    }

    [Fact]
    public void NenhumArquivo_Recusa() =>
        _validator.Validate(new VideoSelectionCandidate([], new LibraryKnownPaths([], []))).Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(LibraryErrorCodes.NoFiles);
}
