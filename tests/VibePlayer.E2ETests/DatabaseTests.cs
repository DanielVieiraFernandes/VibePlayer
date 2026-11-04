using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using VibePlayer.App;
using VibePlayer.App.Resources;
using VibePlayer.App.Views.Shell;
using VibePlayer.Core.Persistence;
using VibePlayer.Infrastructure.Persistence;

namespace VibePlayer.E2ETests;

public sealed class DatabaseTests : IDisposable
{
    private readonly TemporaryAppData _data = new();

    public void Dispose() => _data.Dispose();

    [Fact]
    public void PrimeiraAbertura_CriaBancoComMigrationsAplicadas()
    {
        using var services = _data.BuildServices();

        var result = AppStartup.Prepare(services);

        result.Succeeded.ShouldBeTrue();
        result.BackupPath.ShouldBeNull("Banco novo não tem o que copiar");
        File.Exists(_data.Paths.DatabasePath).ShouldBeTrue();
        using var db = services.GetRequiredService<IDbContextFactory<VibePlayerDbContext>>().CreateDbContext();
        db.Database.GetPendingMigrations().ShouldBeEmpty();
        ExecuteScalar("PRAGMA journal_mode").ShouldBe("wal");
    }

    [Fact]
    public void BancoAntigo_CriaBackupEAplicaMigrationsPreservandoDados()
    {
        CreateOldDatabase();
        using var services = _data.BuildServices();

        var result = AppStartup.Prepare(services);

        result.Succeeded.ShouldBeTrue();
        result.BackupPath.ShouldNotBeNull();
        Path.GetFileName(result.BackupPath).ShouldStartWith("vibePlayer-atualizacao-");
        ExecuteScalar("SELECT Valor FROM DadosAntigos").ShouldBe("progresso do usuário");
        using var db = services.GetRequiredService<IDbContextFactory<VibePlayerDbContext>>().CreateDbContext();
        ExecuteScalar("SELECT count(*) FROM __EFMigrationsHistory").ShouldBe((long)db.Database.GetMigrations().Count());
    }

    [Fact]
    public void FalhaNaMigration_RestauraBackupEMostraErro()
    {
        CreateOldDatabase();
        using var services = _data.BuildServices(overrides => overrides.AddSingleton<IDatabaseInitializer>(sp =>
            new DatabaseInitializer(
                sp.GetRequiredService<IDbContextFactory<VibePlayerDbContext>>(),
                sp.GetRequiredService<IBackupService>(),
                _data.Paths,
                sp.GetRequiredService<ILogger<DatabaseInitializer>>(),
                database =>
                {
                    // Migration que estraga os dados e falha no meio do caminho.
                    database.ExecuteSqlRaw("DELETE FROM DadosAntigos");
                    throw new InvalidOperationException("migration quebrada");
                })));

        var result = AppStartup.Prepare(services);

        result.Succeeded.ShouldBeFalse();
        result.BackupPath.ShouldNotBeNull();
        ExecuteScalar("SELECT Valor FROM DadosAntigos").ShouldBe("progresso do usuário");
        TestAppBuilder.LogSink.Events.ShouldContain(e =>
            e.Level == LogEventLevel.Fatal && e.Exception != null && e.Exception.Message == "migration quebrada");
    }

    [AvaloniaFact]
    public void JanelaDeErro_MostraOndeEstaOBackup()
    {
        var window = new DatabaseErrorWindow(@"C:\dados\backups\vibePlayer-atualizacao.db", @"C:\dados\logs");
        window.Show();

        window.Title.ShouldBe(Strings.AppName);
        window.FindControl<SelectableTextBlock>("MessageText")!.Text!.ShouldContain(@"C:\dados\backups\vibePlayer-atualizacao.db");

        window.Close();
    }

    /// <summary>Banco "de uma versão anterior": tem dados, mas nenhuma das migrations atuais.</summary>
    private void CreateOldDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_data.Paths.DatabasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE DadosAntigos (Valor TEXT); INSERT INTO DadosAntigos VALUES ('progresso do usuário');";
        command.ExecuteNonQuery();
    }

    private object? ExecuteScalar(string sql)
    {
        SqliteConnection.ClearAllPools();
        using var connection = new SqliteConnection($"Data Source={_data.Paths.DatabasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
