using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using VibePlayer.App;
using VibePlayer.App.Services;
using VibePlayer.App.ViewModels;
using VibePlayer.Core.Library;

namespace VibePlayer.E2ETests;

/// <summary>Biblioteca de ponta a ponta: pastas reais em disco, banco real, serviços de produção.</summary>
public sealed class LibraryTests : IDisposable
{
    private readonly TemporaryAppData _data = new();
    private readonly SampleLibrary _sample = new();

    public void Dispose()
    {
        _data.Dispose();
        _sample.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ServiceProvider Open()
    {
        var services = _data.BuildServices();
        AppStartup.Prepare(services).Succeeded.ShouldBeTrue();
        return services;
    }

    [Fact]
    public async Task Reabertura_MantemBiblioteca()
    {
        _sample.AddVideo("Curso/Módulo 1/1 - a.mp4");
        _sample.AddVideo("Curso/Módulo 1/2 - b.mp4");

        IReadOnlyList<CourseNode> before;
        using (var services = Open())
        {
            var library = services.GetRequiredService<ICourseLibrary>();
            await library.AddFolderAsync(_sample.PathOf("Curso"), LibraryRootKind.Course, Token);
            before = await library.GetCoursesAsync(Token);
        }

        using (var services = Open())
        {
            var after = await services.GetRequiredService<ICourseLibrary>().GetCoursesAsync(Token);

            after.Select(c => c.Id).ShouldBe(before.Select(c => c.Id));
            after.Single().Modules.Single().Lessons.Select(l => (l.Id, l.Title))
                .ShouldBe(before.Single().Modules.Single().Lessons.Select(l => (l.Id, l.Title)));
        }
    }

    [Fact]
    public async Task Atualizar_NovoAparece_RemovidoFicaIndisponivel_VoltaComMesmoId()
    {
        _sample.AddVideo("Curso/1 - a.mp4");
        var removed = _sample.AddVideo("Curso/2 - b.mp4");
        using var services = Open();
        var library = services.GetRequiredService<ICourseLibrary>();
        await library.AddFolderAsync(_sample.PathOf("Curso"), LibraryRootKind.Course, Token);
        var original = (await library.GetCoursesAsync(Token)).Single().Lessons.Single(l => l.Title == "2 - b");

        File.Delete(removed);
        _sample.AddVideo("Curso/3 - c.mp4");
        await library.RefreshAsync(cancellationToken: Token);

        var lessons = (await library.GetCoursesAsync(Token)).Single().Lessons;
        lessons.Select(l => l.Title).ShouldBe(["1 - a", "2 - b", "3 - c"]);
        Identity(lessons.Single(l => l.Title == "2 - b")).ShouldBe((original.Id, original.Title, false));

        _sample.AddVideo("Curso/2 - b.mp4");
        await library.RefreshAsync(cancellationToken: Token);

        Identity((await library.GetCoursesAsync(Token)).Single().Lessons.Single(l => l.Title == "2 - b")).ShouldBe(Identity(original));
    }

    [Fact]
    public async Task RaizInacessivel_CursosIndisponiveisRestoFunciona()
    {
        _sample.AddVideo("HD externo/Curso A/a.mp4");
        _sample.AddVideo("Disco/Curso B/b.mp4");
        using var services = Open();
        var library = services.GetRequiredService<ICourseLibrary>();
        await library.AddFolderAsync(_sample.PathOf("HD externo/Curso A"), LibraryRootKind.Course, Token);
        await library.AddFolderAsync(_sample.PathOf("Disco/Curso B"), LibraryRootKind.Course, Token);

        // "Desconecta" o HD.
        Directory.Move(_sample.PathOf("HD externo"), _sample.PathOf("HD guardado"));
        await library.RefreshAsync(cancellationToken: Token);

        var courses = await library.GetCoursesAsync(Token);
        var unavailable = courses.Single(c => c.Title == "Curso A");
        unavailable.IsRootAvailable.ShouldBeFalse();
        unavailable.Lessons.ShouldAllBe(l => !l.IsAvailable);
        courses.Single(c => c.Title == "Curso B").Lessons.ShouldAllBe(l => l.IsAvailable);
        TestAppBuilder.LogSink.Events.ShouldContain(e => e.Level == LogEventLevel.Warning && e.MessageTemplate.Text.Contains("inacessível"));
    }

    [Fact]
    public async Task RemoverCurso_ApagaRegistrosENaoTocaArquivos()
    {
        var files = new[] { _sample.AddVideo("Cursos/Curso A/M/a.mp4"), _sample.AddVideo("Cursos/Curso B/M/b.mp4") };
        using var services = Open();
        var library = services.GetRequiredService<ICourseLibrary>();
        await library.AddFolderAsync(_sample.PathOf("Cursos"), LibraryRootKind.Collection, Token);
        var courseA = (await library.GetCoursesAsync(Token)).Single(c => c.Title == "Curso A");

        await library.RemoveCourseAsync(courseA.Id, Token);
        await library.RefreshAsync(cancellationToken: Token);

        (await library.GetCoursesAsync(Token)).Select(c => c.Title).ShouldBe(["Curso B"], "A atualização não pode trazer de volta o curso removido");
        files.ShouldAllBe(f => File.Exists(f));

        // Removendo o último curso, a pasta sai da biblioteca e pode ser adicionada de novo.
        await library.RemoveCourseAsync((await library.GetCoursesAsync(Token)).Single().Id, Token);
        (await library.GetKnownPathsAsync(Token)).RootPaths.ShouldBeEmpty();
        files.ShouldAllBe(f => File.Exists(f));
    }

    [AvaloniaFact]
    public async Task PastaGrande_AtualizaEmSegundoPlanoComProgresso()
    {
        for (var module = 1; module <= 20; module++)
        {
            for (var lesson = 1; lesson <= 100; lesson++)
            {
                _sample.AddVideo($"Curso/Módulo {module}/Aula {lesson}.mp4");
            }
        }

        using var services = Open();
        await services.GetRequiredService<ICourseLibrary>().AddFolderAsync(_sample.PathOf("Curso"), LibraryRootKind.Course, Token);
        var vm = ActivatorUtilities.CreateInstance<LibraryViewModel>(services);
        var progressTexts = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LibraryViewModel.ProgressText) && vm.ProgressText is { } text)
            {
                progressTexts.Add(text);
            }
        };

        var refresh = vm.RefreshCommand.ExecuteAsync(null);

        // A thread de UI segue livre enquanto a leitura roda.
        vm.IsRefreshing.ShouldBeTrue();
        var uiResponded = false;
        Dispatcher.UIThread.Post(() => uiResponded = true);
        Dispatcher.UIThread.RunJobs();
        uiResponded.ShouldBeTrue();

        await refresh;

        vm.IsRefreshing.ShouldBeFalse();
        vm.Courses.Single().Node.LessonCount.ShouldBe(2000);
        progressTexts.Count.ShouldBeGreaterThan(2, "O progresso deveria ser relatado durante a leitura");
    }

    [Fact]
    public async Task SubpastaSemPermissao_RegistraEContinua()
    {
        // Negar leitura a uma pasta no Windows exige mexer em ACLs; o caso é coberto lá pelos
        // testes unitários do scanner e aqui roda no Linux e no macOS da CI.
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Permissões Unix; no Windows o caso é coberto por LibraryScannerTests.");

        _sample.AddVideo("Curso/Módulo 1/a.mp4");
        _sample.AddVideo("Curso/Trancado/b.mp4");
        using var services = Open();
        var library = services.GetRequiredService<ICourseLibrary>();
        await library.AddFolderAsync(_sample.PathOf("Curso"), LibraryRootKind.Course, Token);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_sample.PathOf("Curso/Trancado"), UnixFileMode.None);
        }

        await library.RefreshAsync(cancellationToken: Token);

        var course = (await library.GetCoursesAsync(Token)).Single();
        course.Modules.Single(m => m.Title == "Módulo 1").Lessons.ShouldAllBe(l => l.IsAvailable);
        TestAppBuilder.LogSink.Events.ShouldContain(e => e.Level == LogEventLevel.Warning && e.MessageTemplate.Text.Contains("puladas"));
    }

    [Fact]
    public async Task AdicionarVideos_CursoNovoOuAvulsos()
    {
        var first = new[] { _sample.AddVideo("Downloads/10 - fim.mp4"), _sample.AddVideo("Downloads/2 - meio.mp4") };
        var loose = _sample.AddVideo("Outro lugar/palestra.mkv");
        using var services = Open();
        var library = services.GetRequiredService<ICourseLibrary>();

        await library.AddVideosAsync(first, new VideoDestination.NewCourse("Minha seleção"), Token);
        await library.AddVideosAsync([loose], new VideoDestination.LooseVideos(), Token);

        var courses = await library.GetCoursesAsync(Token);
        courses.Select(c => (c.Title, c.IsLooseVideos)).ShouldBe([("Minha seleção", false), (string.Empty, true)]);
        courses[0].Lessons.Select(l => l.Title).ShouldBe(["2 - meio", "10 - fim"]);
        courses[1].Lessons.Single().Title.ShouldBe("palestra");
        (await library.GetKnownPathsAsync(Token)).VideoFiles.Count.ShouldBe(3);
    }

    [Fact]
    public async Task RemoverAulaAvulsa()
    {
        var videos = new[] { _sample.AddVideo("Downloads/a.mp4"), _sample.AddVideo("Downloads/b.mp4") };
        using var services = Open();
        var library = services.GetRequiredService<ICourseLibrary>();
        await library.AddVideosAsync(videos, new VideoDestination.LooseVideos(), Token);
        var lessons = (await library.GetCoursesAsync(Token)).Single().Lessons;

        await library.RemoveLessonAsync(lessons[0].Id, Token);
        (await library.GetCoursesAsync(Token)).Single().Lessons.Select(l => l.Title).ShouldBe(["b"]);

        // Sem aulas, o grupo "Vídeos avulsos" deixa de aparecer.
        await library.RemoveLessonAsync(lessons[1].Id, Token);
        (await library.GetCoursesAsync(Token)).ShouldBeEmpty();
        videos.ShouldAllBe(v => File.Exists(v));
    }

    [AvaloniaFact]
    public async Task LocalizarPasta_ReconectaComMesmosIds()
    {
        _sample.AddVideo("Antigo/Curso/M/a.mp4");
        _sample.AddVideo("Antigo/Curso/M/b.mp4");
        var picker = new FakeFilePicker();
        var dialogs = new ScriptedDialogs();
        using var services = OpenWithFakes(picker, dialogs);
        var library = services.GetRequiredService<ICourseLibrary>();
        await library.AddFolderAsync(_sample.PathOf("Antigo/Curso"), LibraryRootKind.Course, Token);
        var before = (await library.GetCoursesAsync(Token)).Single();

        Directory.CreateDirectory(_sample.PathOf("Novo disco"));
        Directory.Move(_sample.PathOf("Antigo/Curso"), _sample.PathOf("Novo disco/Curso"));
        var vm = ActivatorUtilities.CreateInstance<LibraryViewModel>(services);
        await vm.InitializeAsync();
        var course = vm.Courses.Single();
        course.CanLocate.ShouldBeTrue();

        picker.Folders.Enqueue(_sample.PathOf("Novo disco/Curso"));
        await vm.LocateCommand.ExecuteAsync(course);

        vm.ErrorMessage.ShouldBeNull();
        dialogs.Confirmations.ShouldBeEmpty("Aulas conhecidas foram encontradas; não há o que confirmar");
        var after = vm.Courses.Single().Node;
        after.Id.ShouldBe(before.Id);
        after.IsRootAvailable.ShouldBeTrue();
        after.Modules.Single().Lessons.Select(Identity).ShouldBe(before.Modules.Single().Lessons.Select(Identity));
    }

    [AvaloniaFact]
    public async Task LocalizarPasta_SemAulasConhecidas_PedeConfirmacao()
    {
        _sample.AddVideo("Antigo/Curso/a.mp4");
        _sample.AddVideo("Outra coisa/z.mp4");
        var picker = new FakeFilePicker();
        var dialogs = new ScriptedDialogs { ConfirmAnswer = false };
        using var services = OpenWithFakes(picker, dialogs);
        await services.GetRequiredService<ICourseLibrary>().AddFolderAsync(_sample.PathOf("Antigo/Curso"), LibraryRootKind.Course, Token);
        Directory.Delete(_sample.PathOf("Antigo"), recursive: true);
        var vm = ActivatorUtilities.CreateInstance<LibraryViewModel>(services);
        await vm.InitializeAsync();

        picker.Folders.Enqueue(_sample.PathOf("Outra coisa"));
        await vm.LocateCommand.ExecuteAsync(vm.Courses.Single());

        dialogs.Confirmations.ShouldHaveSingleItem();
        vm.Courses.Single().Node.IsRootAvailable.ShouldBeFalse("Cancelado: a pasta não foi trocada");
    }

    // O que identifica a aula; caminho e data do arquivo mudam quando ele é recriado ou movido.
    private static (Guid Id, string Title, bool IsAvailable) Identity(LessonNode lesson) => (lesson.Id, lesson.Title, lesson.IsAvailable);

    private ServiceProvider OpenWithFakes(FakeFilePicker picker, ScriptedDialogs dialogs)
    {
        var services = _data.BuildServices(overrides =>
        {
            overrides.AddSingleton<IFilePicker>(picker);
            overrides.AddSingleton<IDialogService>(dialogs);
        });
        AppStartup.Prepare(services).Succeeded.ShouldBeTrue();
        return services;
    }
}
