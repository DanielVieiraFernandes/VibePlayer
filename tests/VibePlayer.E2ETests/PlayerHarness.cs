using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using VibePlayer.App;
using VibePlayer.App.Services;
using VibePlayer.App.ViewModels;
using VibePlayer.App.Views;
using VibePlayer.Core.Library;

namespace VibePlayer.E2ETests;

/// <summary>
/// Janela real com uma biblioteca de exemplo e o player falso, para os testes do player.
/// Cada "abertura" (<see cref="OpenAsync"/>) cria um container novo sobre a mesma pasta de dados.
/// </summary>
public sealed class PlayerHarness : IDisposable
{
    private readonly TemporaryAppData _data = new();
    private readonly List<ServiceProvider> _services = [];

    public PlayerHarness()
    {
        Sample = new SampleLibrary();
    }

    public SampleLibrary Sample { get; }

    public FakeMediaPlayer Player { get; private set; } = new();

    public FakeMediaInfoReader MediaInfo { get; } = new();

    public FakeThumbnailGenerator Thumbnails { get; } = new();

    public ServiceProvider Services => _services[^1];

    public void Dispose()
    {
        foreach (var services in _services)
        {
            services.Dispose();
        }

        _data.Dispose();
        Sample.Dispose();
    }

    /// <summary>Abre o app (container novo, como uma nova execução) e carrega como na produção.</summary>
    public async Task<MainWindow> OpenAsync(Action<IServiceCollection>? overrides = null)
    {
        Player = new FakeMediaPlayer();
        var services = _data.BuildServices(s =>
        {
            s.UseFakePlayback(Player, MediaInfo, Thumbnails);
            s.AddSingleton<IDialogService>(new ScriptedDialogs());
            overrides?.Invoke(s);
        });
        _services.Add(services);
        AppStartup.Prepare(services).Succeeded.ShouldBeTrue();

        var window = new VibePlayerApp(services).CreateMainWindow();
        window.Show();
        await window.ViewModel().InitializeAsync();
        await SettleAsync();
        return window;
    }

    public async Task AddFolderAsync(string relative, LibraryRootKind kind = LibraryRootKind.Course)
    {
        await Services.GetRequiredService<ICourseLibrary>().AddFolderAsync(Sample.PathOf(relative), kind, TestContext.Current.CancellationToken);
    }

    public static LessonItemViewModel Lesson(MainWindow window, string title) =>
        window.ViewModel().Library.Courses.SelectMany(c => c.AllLessons).Single(l => l.Title == title);

    /// <summary>Seleciona a aula na biblioteca e espera o player abri-la.</summary>
    public static async Task SelectAsync(MainWindow window, string title)
    {
        window.ViewModel().Library.SelectedItem = Lesson(window, title);
        await WaitUntilAsync(() => window.ViewModel().Player.CurrentLesson?.Title == title);
        await SettleAsync();
    }

    /// <summary>Roda o dispatcher até as tarefas agendadas (gravações, recargas) terminarem.</summary>
    public static async Task SettleAsync(int milliseconds = 150)
    {
        var end = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < end)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        WindowTestExtensions.Settle();
    }

    public static async Task WaitUntilAsync(Func<bool> condition, string? what = null)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline, $"Tempo esgotado: {what}");
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
