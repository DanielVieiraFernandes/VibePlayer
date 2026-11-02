using Avalonia;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
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

        // Relógio que avança 1 minuto a cada leitura: desliga na prática o limite de uma notificação
        // de erro a cada 5 s, que senão faria o resultado de um teste depender de qual rodou antes.
        // O limite em si é coberto por NotificationThrottleTests.
        var services = AppComposition.BuildServices(
            paths,
            logger,
            overrides => overrides.AddSingleton<TimeProvider>(new SteppingTimeProvider(TimeSpan.FromMinutes(1))));

        // Mesmo preparo da produção: banco criado com as migrations na pasta temporária.
        var startup = AppStartup.Prepare(services);

        return AppBuilder.Configure(() => new VibePlayerApp(services, startup))
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .WithInterFont();
    }
}
