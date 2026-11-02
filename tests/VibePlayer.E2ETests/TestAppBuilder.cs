using Avalonia;
using Avalonia.Headless;
using Serilog;
using VibePlayer.App;
using VibePlayer.E2ETests;
using VibePlayer.Infrastructure;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace VibePlayer.E2ETests;

/// <summary>
/// Sobe o app real (mesmo composition root de produção) em modo headless: sem janela visível,
/// o que permite rodar na CI de Windows, macOS e Linux sem servidor gráfico.
/// </summary>
/// <remarks>
/// O Avalonia cria uma única instância do app por execução dos testes, por isso os logs vão para
/// um sink estático compartilhado. Cada teste procura no sink um marcador único que ele mesmo
/// gerou, então testes rodando juntos não interferem nas verificações uns dos outros.
/// </remarks>
public static class TestAppBuilder
{
    public static InMemorySink LogSink { get; } = new();

    public static AppBuilder BuildAvaloniaApp()
    {
        // Pasta temporária: os testes nunca tocam na pasta de dados real do usuário.
        var paths = new AppPaths(Path.Combine(Path.GetTempPath(), "VibePlayerE2E", Guid.NewGuid().ToString("N")));

        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(LogSink)
            .CreateLogger();

        var services = AppComposition.BuildServices(paths, logger);

        return AppBuilder.Configure(() => new VibePlayerApp(services))
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .WithInterFont();
    }
}
