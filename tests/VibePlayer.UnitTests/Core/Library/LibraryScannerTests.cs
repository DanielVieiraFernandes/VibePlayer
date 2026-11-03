using VibePlayer.Core.Library;

namespace VibePlayer.UnitTests.Core.Library;

public sealed class LibraryScannerTests
{
    private readonly FakeFileSystem _fs = new();
    private readonly LibraryScanner _scanner;

    public LibraryScannerTests()
    {
        _scanner = new LibraryScanner(_fs);
    }

    [Fact]
    public void Curso_AulasSoltasPrimeiroDepoisModulosEmOrdemNatural()
    {
        _fs.AddFile("Curso/10 - Encerramento.mp4");
        _fs.AddFile("Curso/2 - Boas-vindas.mp4");
        _fs.AddFile("Curso/Módulo 10/1 - x.mkv");
        _fs.AddFile("Curso/Módulo 2/2 - b.mp4");
        _fs.AddFile("Curso/Módulo 2/1 - a.mp4");

        var course = _scanner.ScanRoot(_fs.PathOf("Curso"), LibraryRootKind.Course).Courses.ShouldHaveSingleItem();

        course.Title.ShouldBe("Curso");
        course.RelativePath.ShouldBe(string.Empty);
        course.Modules.Select(m => m.Title).ShouldBe(["Módulo 2", "Módulo 10"]);
        course.Lessons.Select(l => l.Title).ShouldBe(["2 - Boas-vindas", "10 - Encerramento", "1 - a", "2 - b", "1 - x"]);
        course.Lessons.Select(l => l.Position).ShouldBe([0, 1, 2, 3, 4]);
        course.Lessons[2].ModuleRelativePath.ShouldBe("Módulo 2");
        course.Lessons[2].RelativePath.ShouldBe("Módulo 2/1 - a.mp4");
    }

    [Fact]
    public void ModulosAninhados_ViramNomeComposto()
    {
        _fs.AddFile("Curso/Módulo 1/a.mp4");
        _fs.AddFile("Curso/Módulo 1/Parte A/b.mp4");
        _fs.AddFile("Curso/Módulo 1/Parte A/Extra/c.mp4");
        _fs.AddDirectory("Curso/Vazio/Também vazio");

        var course = _scanner.ScanRoot(_fs.PathOf("Curso"), LibraryRootKind.Course).Courses.ShouldHaveSingleItem();

        course.Modules.Select(m => m.Title).ShouldBe(["Módulo 1", "Módulo 1 / Parte A", "Módulo 1 / Parte A / Extra"]);
        course.Modules.Select(m => m.RelativePath).ShouldBe(["Módulo 1", "Módulo 1/Parte A", "Módulo 1/Parte A/Extra"]);
    }

    [Fact]
    public void IgnoraNaoVideosOcultosSistemaELinks()
    {
        _fs.AddFile("Curso/aula.mp4");
        _fs.AddFile("Curso/apostila.pdf");
        _fs.AddFile("Curso/legenda.srt");
        _fs.AddFile("Curso/.DS_Store");
        _fs.AddFile("Curso/Thumbs.db");
        _fs.AddFile("Curso/oculto.mp4", hidden: true);
        _fs.AddFile("Curso/atalho.mp4", link: true);
        _fs.AddFile("Curso/@eaDir/miniatura.mp4");
        _fs.AddFile("Curso/.cache/x.mp4");
        _fs.AddFile("Curso/Junção/y.mp4");
        _fs.AddDirectory("Curso/Junção", link: true);

        var course = _scanner.ScanRoot(_fs.PathOf("Curso"), LibraryRootKind.Course).Courses.ShouldHaveSingleItem();

        course.Lessons.Select(l => l.Title).ShouldBe(["aula"]);
        course.Modules.ShouldBeEmpty();
    }

    [Fact]
    public void PastaDeCursos_CadaSubpastaComVideosEhUmCurso()
    {
        _fs.AddFile("Cursos/solto.mp4");
        _fs.AddFile("Cursos/Curso B/Módulo/a.mp4");
        _fs.AddFile("Cursos/Curso A/a.mp4");
        _fs.AddDirectory("Cursos/Sem vídeos");

        var scan = _scanner.ScanRoot(_fs.PathOf("Cursos"), LibraryRootKind.Collection);

        scan.Courses.Select(c => c.Title).ShouldBe(["Curso A", "Curso B"]);
        scan.Courses[1].RelativePath.ShouldBe("Curso B");
        scan.IgnoredLooseVideos.ShouldBe(1);
    }

    [Fact]
    public void SubpastaIlegivel_EhPuladaEContada()
    {
        _fs.AddFile("Curso/Módulo 1/a.mp4");
        _fs.AddFile("Curso/Trancado/b.mp4");
        _fs.MakeUnreadable("Curso/Trancado");

        var scan = _scanner.ScanRoot(_fs.PathOf("Curso"), LibraryRootKind.Course);

        scan.Courses.ShouldHaveSingleItem().Lessons.Select(l => l.Title).ShouldBe(["a"]);
        scan.SkippedDirectories.ShouldBe(1);
    }

    [Fact]
    public void RaizInacessivel_Lanca()
    {
        _fs.AddDirectory("Curso");
        _fs.MakeUnreadable("Curso");

        Should.Throw<UnauthorizedAccessException>(() => _scanner.ScanRoot(_fs.PathOf("Curso"), LibraryRootKind.Course));
    }

    [Fact]
    public void Progresso_AvisaACadaAula()
    {
        _fs.AddFile("Curso/a.mp4");
        _fs.AddFile("Curso/M/b.mp4");
        var found = 0;

        _scanner.ScanRoot(_fs.PathOf("Curso"), LibraryRootKind.Course, () => found++);

        found.ShouldBe(2);
    }
}
