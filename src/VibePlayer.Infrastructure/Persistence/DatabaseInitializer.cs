using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using VibePlayer.Core;
using VibePlayer.Core.Persistence;

namespace VibePlayer.Infrastructure.Persistence;

/// <summary>
/// Cria ou atualiza o banco na inicialização, com backup antes e restauração se der errado.
/// </summary>
/// <remarks>
/// Fluxo:
/// <list type="number">
/// <item>Sem migrations pendentes → nada a fazer (caso de quase toda abertura).</item>
/// <item>Pendentes num banco que já tem dados → backup de atualização. Se o backup falhar, a
/// migration não roda: atualizar sem rede de segurança é o que a rotina existe para evitar.</item>
/// <item>Migrate. Se falhar, as conexões são fechadas e o backup volta para o lugar; o app avisa
/// e fecha, e uma versão corrigida encontra os dados intactos. Abrir com um banco novo esconderia
/// o problema e o usuário perderia tudo de vista.</item>
/// </list>
/// Banco de uma versão mais nova do app (o usuário voltou de versão): o EF ignora migrations que
/// não conhece; só registramos o aviso e seguimos.
/// </remarks>
public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IDbContextFactory<VibePlayerDbContext> _contextFactory;
    private readonly IBackupService _backups;
    private readonly IAppPaths _paths;
    private readonly ILogger<DatabaseInitializer> _logger;
    private readonly Action<DatabaseFacade> _migrate;

    public DatabaseInitializer(
        IDbContextFactory<VibePlayerDbContext> contextFactory,
        IBackupService backups,
        IAppPaths paths,
        ILogger<DatabaseInitializer> logger)
        : this(contextFactory, backups, paths, logger, database => database.Migrate())
    {
    }

    /// <param name="migrate">Só para testes: simula uma migration que falha no meio.</param>
    public DatabaseInitializer(
        IDbContextFactory<VibePlayerDbContext> contextFactory,
        IBackupService backups,
        IAppPaths paths,
        ILogger<DatabaseInitializer> logger,
        Action<DatabaseFacade> migrate)
    {
        _contextFactory = contextFactory;
        _backups = backups;
        _paths = paths;
        _logger = logger;
        _migrate = migrate;
    }

    public DatabaseInitializationResult Initialize()
    {
        // Antes de abrir qualquer conexão: abrir já cria o arquivo vazio.
        var hadData = File.Exists(_paths.DatabasePath) && new FileInfo(_paths.DatabasePath).Length > 0;
        string? backupPath = null;

        try
        {
            List<string> pending;
            using (var db = _contextFactory.CreateDbContext())
            {
                pending = db.Database.GetPendingMigrations().ToList();
                WarnIfDatabaseIsNewer(db);
            }

            if (pending.Count == 0)
            {
                return DatabaseInitializationResult.Success();
            }

            if (hadData)
            {
                backupPath = _backups.CreateBackup(BackupKind.PreMigration);
            }

            var stopwatch = Stopwatch.StartNew();
            using (var db = _contextFactory.CreateDbContext())
            {
                _migrate(db.Database);

                // WAL fica gravado no arquivo, então basta definir uma vez: leituras da UI não
                // esperam as gravações em segundo plano (transcrição, chat).
                db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
            }

            _logger.LogInformation("Banco atualizado: {Count} migrations aplicadas em {Elapsed}", pending.Count, stopwatch.Elapsed);
            _logger.LogDebug("Migrations aplicadas: {Migrations}", pending);
            return DatabaseInitializationResult.Success(backupPath);
        }
        catch (Exception ex)
        {
            Recover(ex, hadData, backupPath);
            return DatabaseInitializationResult.Failure(backupPath);
        }
    }

    private void WarnIfDatabaseIsNewer(VibePlayerDbContext db)
    {
        var unknown = db.Database.GetAppliedMigrations().Except(db.Database.GetMigrations()).ToList();
        if (unknown.Count > 0)
        {
            _logger.LogWarning(
                "O banco tem {Count} migrations de uma versão mais nova do app; elas serão ignoradas: {Migrations}",
                unknown.Count,
                unknown);
        }
    }

    private void Recover(Exception error, bool hadData, string? backupPath)
    {
        // Fecha as conexões do pool: no Windows elas travam o arquivo e a restauração falharia.
        SqliteConnection.ClearAllPools();

        try
        {
            if (backupPath is not null)
            {
                RestoreBackup(backupPath);
                _logger.LogCritical(error, "Falha ao atualizar o banco; o backup {BackupPath} foi restaurado", backupPath);
            }
            else if (!hadData)
            {
                // Primeira abertura: não havia dados; um banco criado pela metade só atrapalharia
                // a próxima tentativa.
                DeleteDatabaseFiles();
                _logger.LogCritical(error, "Falha ao criar o banco");
            }
            else
            {
                // Falhou antes do backup (banco ilegível ou backup impossível): nada foi alterado.
                _logger.LogCritical(error, "Falha ao preparar o banco; nenhum dado foi alterado");
            }
        }
        catch (Exception restoreError)
        {
            _logger.LogCritical(
                restoreError,
                "Falha ao restaurar o backup {BackupPath} depois do erro na atualização {Error}; restaure manualmente",
                backupPath,
                error.Message);
        }
    }

    private void RestoreBackup(string backupPath)
    {
        // -wal e -shm são do banco que falhou; misturados ao backup restaurado, corromperiam o estado.
        DeleteDatabaseFiles();
        File.Copy(backupPath, _paths.DatabasePath, overwrite: true);
    }

    private void DeleteDatabaseFiles()
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            File.Delete(_paths.DatabasePath + suffix);
        }
    }
}
