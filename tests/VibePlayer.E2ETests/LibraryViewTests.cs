using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using VibePlayer.App;
using VibePlayer.App.Resources;
using VibePlayer.App.Services;
using VibePlayer.App.ViewModels;
using VibePlayer.App.Views;
using VibePlayer.App.Views.Library;
using VibePlayer.Core.Library;

namespace VibePlayer.E2ETests;

/// <summary>A aba Aulas na janela real, com seletor de arquivos e diálogos respondidos pelo teste.</summary>
public sealed class LibraryViewTests : IDisposable
{
    private readonly TemporaryAppData _data = new();
    private readonly SampleLibrary _sample = new();
    private readonly FakeFilePicker _picker = new();
    private readonly ScriptedDialogs _dialogs = new();
    private ServiceProvider? _services;

    public void Dispose()
    {
        _services?.Dispose();
        _data.Dispose();
        _sample.Dispose();
    }

    [AvaloniaFact]
    public async Task AdicionarPasta_PerguntaInterpretacaoComPrevia()
    {
        _sample.AddVideo("Cursos/Curso A/Módulo 1/a.mp4");
        _sample.AddVideo("Cursos/Curso B/Módulo 1/b.mp4");
        var window = await OpenAsync();
        _picker.Folders.Enqueue(_sample.PathOf("Cursos"));
        _dialogs.FolderAnswer = _ => LibraryRootKind.Collection;

        window.Click(window.Find<Button>("EmptyAddFolderButton"));
        await WaitUntilAsync(() => window.ViewModel().Library.HasCourses);

        var preview = _dialogs.FolderPreviews.ShouldHaveSingleItem();
        preview.Suggested.ShouldBe(LibraryRootKind.Collection);
        preview.AsCollection.Courses.Count.ShouldBe(2);
        window.ViewModel().Library.Courses.Select(c => c.Title).ShouldBe(["Curso A", "Curso B"]);
        window.Close();
    }

    [AvaloniaFact]
    public async Task AdicionarPasta_Cancelado_NaoGravaNada()
    {
        _sample.AddVideo("Curso/a.mp4");
        var window = await OpenAsync();
        _picker.Folders.Enqueue(_sample.PathOf("Curso"));
        _dialogs.FolderAnswer = _ => null;

        await window.ViewModel().Library.AddFolderCommand.ExecuteAsync(null);

        _dialogs.FolderPreviews.ShouldHaveSingleItem();
        window.ViewModel().Library.HasCourses.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public async Task AdicionarPastaInvalida_MostraFaixaDeErro()
    {
        Directory.CreateDirectory(_sample.PathOf("Sem vídeos"));
        var window = await OpenAsync();
        _picker.Folders.Enqueue(_sample.PathOf("Sem vídeos"));

        await window.ViewModel().Library.AddFolderCommand.ExecuteAsync(null);
        WindowTestExtensions.Settle();

        _dialogs.FolderPreviews.ShouldBeEmpty("Pasta inválida não chega à prévia");
        window.ShowsText(Strings.Library_Error_NoVideos).ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public async Task DialogoDePasta_TrocarOpcaoAtualizaPreviaEConfirmar()
    {
        _sample.AddVideo("Curso/Módulo 1/a.mp4");
        _sample.AddVideo("Curso/Módulo 2/b.mp4");
        var owner = await OpenAsync();
        var preview = new LibraryScanner(new VibePlayer.Infrastructure.FileSystem.PhysicalFileSystemReader()).Preview(_sample.PathOf("Curso"));
        var dialog = new AddLibraryFolderDialog { DataContext = new AddLibraryFolderViewModel(preview) };

        var result = dialog.ShowDialog<LibraryRootKind?>(owner);
        WindowTestExtensions.Settle();

        dialog.Find<RadioButton>("CourseOption").IsChecked.ShouldBe(true, "A sugestão vem marcada");
        dialog.ShowsText("Curso — 2 aulas").ShouldBeTrue();

        dialog.Find<RadioButton>("CollectionOption").IsChecked = true;
        WindowTestExtensions.Settle();
        dialog.ShowsText("Módulo 1 — 1 aula").ShouldBeTrue();

        dialog.Find<Button>("ConfirmButton").Command.ShouldBeNull();
        DialogClick(dialog, dialog.Find<Button>("ConfirmButton"));
        (await result).ShouldBe(LibraryRootKind.Collection);
        owner.Close();
    }

    [AvaloniaFact]
    public async Task SelecionarAula_MostraTituloNoPlayer()
    {
        _sample.AddVideo("Fotografia/1 - Luz.mp4");
        var window = await OpenWithFolderAsync("Fotografia");
        var library = window.ViewModel().Library;

        library.SelectedItem = library.Courses.Single().Children.Single();
        WindowTestExtensions.Settle();

        window.ShowsText("1 - Luz").ShouldBeTrue();
        window.ShowsText("Fotografia").ShouldBeTrue();
        window.ShowsText(Strings.Player_EmptyTitle).ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public async Task AulaIndisponivel_NaoPodeSerSelecionada()
    {
        var file = _sample.AddVideo("Curso/1 - a.mp4");
        _sample.AddVideo("Curso/2 - b.mp4");
        var window = await OpenWithFolderAsync("Curso");
        var library = window.ViewModel().Library;
        File.Delete(file);
        await library.RefreshCommand.ExecuteAsync(null);

        var unavailable = library.Courses.Single().Children.First();
        unavailable.IsAvailable.ShouldBeFalse();
        library.SelectedItem = unavailable;
        WindowTestExtensions.Settle();

        library.CurrentLesson.ShouldBeNull();
        window.ShowsText(Strings.Player_EmptyTitle).ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public async Task Teclado_NavegaESeleciona()
    {
        _sample.AddVideo("Curso/1 - a.mp4");
        _sample.AddVideo("Curso/2 - b.mp4");
        var window = await OpenWithFolderAsync("Curso");
        var tree = window.Find<TreeView>("Tree");
        var course = tree.GetVisualDescendants().OfType<TreeViewItem>().First();

        // Com foco mas sem seleção, a primeira seta do TreeView só seleciona o item com foco; por
        // isso o teste parte do curso já selecionado, como fica depois de o usuário clicar nele.
        window.ViewModel().Library.SelectedItem = window.ViewModel().Library.Courses.Single();
        WindowTestExtensions.Settle();
        course.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.ArrowRight);

        Press(window, PhysicalKey.ArrowDown);
        window.ViewModel().Library.CurrentLesson?.Title.ShouldBe("1 - a");

        Press(window, PhysicalKey.ArrowDown);
        window.ViewModel().Library.CurrentLesson?.Title.ShouldBe("2 - b");
        window.Close();
    }

    [AvaloniaFact]
    public async Task CliqueDireito_EmQualquerPontoDaLinha_AbreAcoesDoCurso()
    {
        _sample.AddVideo("Curso/M/a.mp4");
        var window = await OpenWithFolderAsync("Curso");
        var view = window.GetVisualDescendants().OfType<LibraryView>().Single();
        var row = window.GetVisualDescendants().OfType<TreeViewItem>().First();

        // Pontos sem texto: logo depois do recuo (antes do título) e perto da borda direita.
        foreach (var x in new[] { 4.0, row.Bounds.Width - 48 })
        {
            RightClick(window, row, new Point(x, 12));

            var menu = view.OpenActionsMenu.ShouldNotBeNull($"Clique direito em x={x} não abriu o menu");
            menu.Items.OfType<MenuItem>().Select(i => i.Header).ShouldBe([Strings.Library_TranscribeCourse, Strings.Library_Remove]);
            menu.Close();
        }

        window.Close();
    }

    [AvaloniaFact]
    public async Task BotaoDeAcoes_AbreMenuEItemSemAcoesNaoAbreODoCurso()
    {
        _sample.AddVideo("Curso/M/a.mp4");
        var window = await OpenWithFolderAsync("Curso");
        var view = window.GetVisualDescendants().OfType<LibraryView>().Single();
        var course = window.GetVisualDescendants().OfType<TreeViewItem>().First();

        var actions = course.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("rowActions"));
        actions.IsEffectivelyVisible.ShouldBeTrue();
        window.Click(actions);
        view.OpenActionsMenu.ShouldNotBeNull().Items.OfType<MenuItem>().Select(i => i.Header).ShouldBe([Strings.Library_TranscribeCourse, Strings.Library_Remove]);
        view.OpenActionsMenu!.Close();

        // Módulo não tem ações: o clique direito nele não pode abrir o menu do curso.
        course.IsExpanded = true;
        WindowTestExtensions.Settle();
        var module = course.GetVisualDescendants().OfType<TreeViewItem>().First();
        RightClick(window, module, new Point(module.Bounds.Width - 48, 12));
        view.OpenActionsMenu.ShouldBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public async Task PastaIndisponivel_MostraLocalizarPastaNaLinha()
    {
        _sample.AddVideo("HD/Curso/a.mp4");
        var window = await OpenWithFolderAsync("HD/Curso");
        Directory.Delete(_sample.PathOf("HD"), recursive: true);
        await window.ViewModel().Library.RefreshCommand.ExecuteAsync(null);
        WindowTestExtensions.Settle();

        var link = window.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("link"));
        link.IsEffectivelyVisible.ShouldBeTrue();
        window.ShowsText(Strings.Library_Locate).ShouldBeTrue();

        _picker.Folders.Enqueue(null);
        window.Click(link);
        await WaitUntilAsync(() => _picker.Folders.Count == 0);
        window.Close();
    }

    private static void RightClick(MainWindow window, Control target, Point point)
    {
        var position = target.TranslatePoint(point, window)!.Value;
        window.MouseDown(position, MouseButton.Right);
        window.MouseUp(position, MouseButton.Right);
        WindowTestExtensions.Settle();
    }

    private async Task<MainWindow> OpenAsync()
    {
        _services = _data.BuildServices(overrides =>
        {
            overrides.AddSingleton<IFilePicker>(_picker);
            overrides.AddSingleton<IDialogService>(_dialogs);
        });
        AppStartup.Prepare(_services).Succeeded.ShouldBeTrue();

        var window = new VibePlayerApp(_services).CreateMainWindow();
        window.Show();
        await window.ViewModel().Library.InitializeAsync();
        WindowTestExtensions.Settle();
        return window;
    }

    private async Task<MainWindow> OpenWithFolderAsync(string relative)
    {
        var window = await OpenAsync();
        _picker.Folders.Enqueue(_sample.PathOf(relative));
        await window.ViewModel().Library.AddFolderCommand.ExecuteAsync(null);
        WindowTestExtensions.Settle();
        window.ViewModel().Library.HasCourses.ShouldBeTrue();
        return window;
    }

    private static void Press(MainWindow window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        WindowTestExtensions.Settle();
    }

    private static void DialogClick(Window dialog, Control control)
    {
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), dialog)!.Value;
        dialog.MouseDown(center, MouseButton.Left);
        dialog.MouseUp(center, MouseButton.Left);
        WindowTestExtensions.Settle();
    }

    /// <summary>Espera um comando assíncrono disparado por clique terminar, rodando o dispatcher.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline, "A condição não foi atendida a tempo");
            await Task.Delay(20, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
