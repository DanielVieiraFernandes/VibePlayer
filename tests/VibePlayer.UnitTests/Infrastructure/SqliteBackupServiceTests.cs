using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using VibePlayer.Core.Persistence;
using VibePlayer.Infrastructure;
using VibePlayer.Infrastructure.Persistence;

namespace VibePlayer.UnitTests.Infrastructure;

/// <summary>
/// O produto do backup é um arquivo, então estes testes usam uma pasta temporária própria
/// (exceção à regra de unitários sem disco, registrada na spec 0004).
/// </summary>
public sealed class SqliteBackupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "VibePlayerTests", Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
    private readonly ListLogger<SqliteBackupService> _logger = new();
    private readonly SqliteBackupService _service;

    public SqliteBackupServiceTests()
    {
        _paths = new AppPaths(_root);
        _service = new SqliteBackupService(_paths, _clock, _logger);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task PrimeiraAberturaDoDia_CriaBackupValido()
    {
        CreateDatabase("valor original");

        await _service.RunDailyIfDueAsync(Token);

        var backup = Directory.GetFiles(_paths.BackupsDirectory).ShouldHaveSingleItem();
        Path.GetFileName(backup).ShouldBe("vibePlayer-diario-20261102-090000.db");
        ReadValue(backup).ShouldBe("valor original");
    }

    [Fact]
    public async Task SegundaAberturaDoDia_NaoCriaBackup()
    {
        CreateDatabase("valor");
        await _service.RunDailyIfDueAsync(Token);

        _clock.Advance(TimeSpan.FromHours(10));
        await _service.RunDailyIfDueAsync(Token);
        Directory.GetFiles(_paths.BackupsDirectory).Length.ShouldBe(1);

        // Virou o dia: novo backup, mesmo com menos de 24 h desde o anterior.
        _clock.Advance(TimeSpan.FromHours(5));
        await _service.RunDailyIfDueAsync(Token);
        Directory.GetFiles(_paths.BackupsDirectory).Length.ShouldBe(2);
    }

    [Fact]
    public void BackupDeAtualizacao_LevaAVersaoNoNome()
    {
        CreateDatabase("valor");

        var backup = _service.CreateBackup(BackupKind.PreMigration);

        Path.GetFileName(backup).ShouldStartWith("vibePlayer-atualizacao-20261102-090000-v");
    }

    [Fact]
    public void SemBanco_NaoCriaBackup()
    {
        _service.CreateBackup(BackupKind.Daily).ShouldBeNull();
        Directory.GetFiles(_paths.BackupsDirectory).ShouldBeEmpty();
    }

    [Fact]
    public void Limpeza_MantemOsMaisRecentesPorTipo()
    {
        CreateDatabase("valor");
        var userCopy = Path.Combine(_paths.BackupsDirectory, "minha-copia.db");
        File.WriteAllText(userCopy, "do usuário");
        var leftover = Path.Combine(_paths.BackupsDirectory, "vibePlayer-diario-20261001-000000.db.tmp");
        File.WriteAllText(leftover, "backup interrompido");

        for (var i = 0; i < 4; i++)
        {
            _service.CreateBackup(BackupKind.PreMigration);
            _clock.Advance(TimeSpan.FromMinutes(1));
        }

        for (var i = 0; i < 8; i++)
        {
            _service.CreateBackup(BackupKind.Daily);
            _clock.Advance(TimeSpan.FromDays(1));
        }

        var names = Directory.GetFiles(_paths.BackupsDirectory).Select(Path.GetFileName).ToList();
        names.Count(n => n!.StartsWith("vibePlayer-diario-", StringComparison.Ordinal)).ShouldBe(SqliteBackupService.DailyToKeep);
        names.Count(n => n!.StartsWith("vibePlayer-atualizacao-", StringComparison.Ordinal)).ShouldBe(SqliteBackupService.PreMigrationToKeep);

        // Os mais antigos de cada tipo saíram.
        names.ShouldNotContain("vibePlayer-diario-20261102-090400.db");
        names.ShouldNotContain(n => n!.StartsWith("vibePlayer-atualizacao-20261102-090000", StringComparison.Ordinal));

        // Arquivo fora do padrão fica; resto de backup interrompido sai.
        names.ShouldContain("minha-copia.db");
        names.ShouldNotContain(Path.GetFileName(leftover));
    }

    [Fact]
    public async Task FalhaNoBackup_RegistraErroEPreservaBackupsAnteriores()
    {
        CreateDatabase("valor");
        await _service.RunDailyIfDueAsync(Token);
        var previous = Directory.GetFiles(_paths.BackupsDirectory).ShouldHaveSingleItem();

        // Banco corrompido: o backup falha na leitura e não pode apagar o backup bom.
        SqliteConnection.ClearAllPools();
        File.WriteAllText(_paths.DatabasePath, "isto não é um banco SQLite, mas tem tamanho suficiente para o cabeçalho");
        _clock.Advance(TimeSpan.FromDays(1));

        await _service.RunDailyIfDueAsync(Token);

        Directory.GetFiles(_paths.BackupsDirectory).ShouldBe([previous]);
        ReadValue(previous).ShouldBe("valor");
        _logger.Entries.ShouldContain(e => e.Level == LogLevel.Error);
    }

    private void CreateDatabase(string value)
    {
        using var connection = new SqliteConnection($"Data Source={_paths.DatabasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        // WAL, como o banco real do app (ver DatabaseInitializer).
        command.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE Dados (Valor TEXT); INSERT INTO Dados VALUES ($valor);";
        command.Parameters.AddWithValue("$valor", value);
        command.ExecuteNonQuery();
    }

    private static string? ReadValue(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Valor FROM Dados";
        return command.ExecuteScalar() as string;
    }
}
