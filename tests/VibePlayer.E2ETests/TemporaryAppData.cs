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
    /// <remarks>Reprodução sempre falsa (ver FakeMediaPlayer); <paramref name="overrides"/> vem depois e pode trocar os falsos.</remarks>
    public ServiceProvider BuildServices(Action<IServiceCollection>? overrides = null) =>
        AppComposition.BuildServices(
            Paths,
            new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(TestAppBuilder.LogSink).CreateLogger(),
            services =>
            {
                services.UseFakePlayback();
                overrides?.Invoke(services);
            });

    public void Dispose()
    {
        // Algumas tentativas: trabalho em segundo plano do app (durações, miniaturas) pode estar
        // terminando e reabrir o banco. O pool do SQLite mantém o arquivo aberto, e no Windows a
        // pasta só sai com ele limpo. Por ser temporária, não sair no fim não é falha de teste.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(Paths.DataDirectory, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }
}
