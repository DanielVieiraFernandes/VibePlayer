using VibePlayer.Core.Library;

namespace VibePlayer.UnitTests.Core.Library;

public sealed class LibraryDetectionTests
{
    private readonly FakeFileSystem _fs = new();
    private readonly LibraryScanner _scanner;

    public LibraryDetectionTests()
    {
        _scanner = new LibraryScanner(_fs);
    }

    [Fact]
    public void SubpastasComModulos_SugereVariosCursos()
    {
        _fs.AddFile("Cursos/Curso A/Módulo 1/a.mp4");
        _fs.AddFile("Cursos/Curso B/Módulo 1/a.mp4");
        _fs.AddFile("Cursos/Curso C/Módulo 1/a.mp4");

        var preview = _scanner.Preview(_fs.PathOf("Cursos"));

        preview.Suggested.ShouldBe(LibraryRootKind.Collection);
        preview.AsCollection.Courses.Select(c => c.Title).ShouldBe(["Curso A", "Curso B", "Curso C"]);
    }

    [Fact]
    public void ModulosComVideos_SugereUmCurso()
    {
        _fs.AddFile("Curso/Módulo 1/a.mp4");
        _fs.AddFile("Curso/Módulo 2/b.mp4");
        _fs.AddFile("Curso/intro.mp4");

        var preview = _scanner.Preview(_fs.PathOf("Curso"));

        preview.Suggested.ShouldBe(LibraryRootKind.Course);
        var course = preview.AsCourse.Courses.ShouldHaveSingleItem();
        course.ModuleCount.ShouldBe(2);
        course.LessonCount.ShouldBe(3);
    }

    [Fact]
    public void Previa_TrazAsDuasInterpretacoes()
    {
        _fs.AddFile("Curso/Módulo 1/a.mp4");
        _fs.AddFile("Curso/Módulo 2/b.mp4");

        var preview = _scanner.Preview(_fs.PathOf("Curso"));

        preview.AsCourse.Courses.Count.ShouldBe(1);
        preview.AsCollection.Courses.Select(c => c.Title).ShouldBe(["Módulo 1", "Módulo 2"]);
    }
}
