using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using VibePlayer.App;
using VibePlayer.Infrastructure;

namespace VibePlayer.E2ETests;

/// <summary>
/// Pasta de dados temporária própria do teste, para cenários que precisam de "aberturas" do app
/// em sequência sobre o mesmo banco (o app compartilhado do TestAppBuilder não serve para isso).
/// </summary>
public sealed class TemporaryAppData : IDisposable
{
    public TemporaryAppData()
    {
        Paths = new AppPaths(Path.Combine(Path.GetTempPath(), "VibePlayerE2E", Guid.NewGuid().ToString("N")));
    }

    public AppPaths Paths { get; }

    /// <summary>Uma "abertura" do app: mesmo composition root de produção, sobre esta pasta.</summary>
    public ServiceProvider BuildServices(Action<IServiceCollection>? overrides = null) =>
        AppComposition.BuildServices(
            Paths,
            new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(TestAppBuilder.LogSink).CreateLogger(),
            overrides);

    public void Dispose()
    {
        // O pool de conexões do SQLite mantém o arquivo aberto; sem isso a pasta não sai no Windows.
        SqliteConnection.ClearAllPools();
        Directory.Delete(Paths.DataDirectory, recursive: true);
    }
}
