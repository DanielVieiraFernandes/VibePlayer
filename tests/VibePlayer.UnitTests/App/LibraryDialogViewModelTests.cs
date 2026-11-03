using VibePlayer.App.ViewModels;
using VibePlayer.Core.Library;

namespace VibePlayer.UnitTests.App;

public sealed class LibraryDialogViewModelTests
{
    private static FolderPreview Preview(LibraryRootKind suggested, int collectionCourses, int ignored = 0) => new(
        "/cursos",
        suggested,
        new InterpretationPreview([new PreviewCourse("Cursos", 3, 30)], 0),
        new InterpretationPreview(Enumerable.Range(1, collectionCourses).Select(i => new PreviewCourse($"Curso {i}", 1, 1)).ToList(), ignored));

    [Fact]
    public void Pasta_AbreComASugestaoMarcadaETrocaAPrevia()
    {
        var vm = new AddLibraryFolderViewModel(Preview(LibraryRootKind.Collection, 3));

        vm.IsCollection.ShouldBeTrue();
        vm.SelectedKind.ShouldBe(LibraryRootKind.Collection);
        vm.PreviewLines.ShouldBe(["Curso 1 — 1 aula", "Curso 2 — 1 aula", "Curso 3 — 1 aula"]);

        vm.IsCourse = true;

        vm.SelectedKind.ShouldBe(LibraryRootKind.Course);
        vm.PreviewLines.ShouldBe(["Cursos — 30 aulas"]);
        vm.CourseSummary.ShouldBe("1 curso com 3 módulos e 30 aulas");
    }

    [Fact]
    public void Pasta_PreviaLongaResumeEAvisaVideosSoltos()
    {
        var vm = new AddLibraryFolderViewModel(Preview(LibraryRootKind.Collection, 12, ignored: 2));

        vm.PreviewLines.Count.ShouldBe(9);
        vm.PreviewLines[^1].ShouldBe("e mais 4");
        vm.HasIgnoredLoose.ShouldBeTrue();

        vm.IsCourse = true;
        vm.HasIgnoredLoose.ShouldBeFalse();
    }

    [Fact]
    public void Pasta_OpcaoSemCursos_NaoPodeConfirmar()
    {
        var vm = new AddLibraryFolderViewModel(Preview(LibraryRootKind.Course, 0));

        vm.CanConfirm.ShouldBeTrue();
        vm.IsCollection = true;
        vm.CanConfirm.ShouldBeFalse();
    }

    [Fact]
    public void Videos_SugereAPastaEmComumComoNome()
    {
        var folder = Path.Combine(Path.GetTempPath(), "Curso de Fotografia");
        var vm = new AddVideosViewModel([Path.Combine(folder, "2.mp4"), Path.Combine(folder, "1.mp4")]);

        vm.CourseName.ShouldBe("Curso de Fotografia");
        vm.Destination.ShouldBe(new VideoDestination.NewCourse("Curso de Fotografia"));
    }

    [Fact]
    public void Videos_SemPastaEmComum_SugereOPrimeiroVideo()
    {
        var vm = new AddVideosViewModel([Path.Combine(Path.GetTempPath(), "b", "10 - Fim.mp4"), Path.Combine(Path.GetTempPath(), "a", "2 - Começo.mp4")]);

        vm.CourseName.ShouldBe("2 - Começo");
    }

    [Fact]
    public void Videos_CursoNovoSemNome_NaoPodeConfirmar_AvulsosPode()
    {
        var vm = new AddVideosViewModel([Path.Combine(Path.GetTempPath(), "x", "a.mp4")]) { CourseName = "  " };

        vm.CanConfirm.ShouldBeFalse();

        vm.IsLooseVideos = true;
        vm.CanConfirm.ShouldBeTrue();
        vm.Destination.ShouldBeOfType<VideoDestination.LooseVideos>();
    }
}
