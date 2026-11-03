using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using VibePlayer.Core;
using VibePlayer.Core.Persistence;

namespace VibePlayer.Infrastructure.Persistence;

/// <summary>
/// Backups do banco em <c>backups/</c>, com nomes como
/// <c>vibePlayer-diario-20261102-091512.db</c> e <c>vibePlayer-atualizacao-20261102-091510-v0.3.0.db</c>.
/// </summary>
/// <remarks>
/// Desenho:
/// <list type="number">
/// <item>Cópia pela API de backup online do SQLite, não por cópia de arquivo. Ela lê um estado
/// consistente mesmo com o app gravando e inclui o que ainda está no <c>-wal</c>; copiar só o
/// <c>.db</c> pegaria o banco pela metade.</item>
/// <item>Grava num <c>.tmp</c>, confere com <c>integrity_check</c> e só então renomeia para
/// <c>.db</c>. Um backup interrompido ou com defeito nunca parece válido.</item>
/// <item>A retenção roda só depois de um backup válido: se algo vai mal (banco corrompido, disco
/// cheio), os backups antigos são justamente o que se quer preservar.</item>
/// <item>Data e ordem vêm do nome do arquivo, não do sistema de arquivos, cujas datas mudam ao
/// copiar a pasta para outro computador. Arquivos fora do padrão de nome nunca são apagados.</item>
/// </list>
/// </remarks>
public sealed partial class SqliteBackupService(IAppPaths paths, TimeProvider timeProvider, ILogger<SqliteBackupService> logger)
    : IBackupService
{
    public const int DailyToKeep = 7;
    public const int PreMigrationToKeep = 3;

    private const string TimestampFormat = "yyyyMMdd-HHmmss";

    // Prefixos em português porque o usuário vê esses nomes ao restaurar um backup à mão.
    private static readonly Dictionary<BackupKind, string> KindNames = new()
    {
        [BackupKind.Daily] = "diario",
        [BackupKind.PreMigration] = "atualizacao",
    };

    private readonly Lock _lock = new();

    [GeneratedRegex(@"^vibePlayer-(?<kind>diario|atualizacao)-(?<timestamp>\d{8}-\d{6})(-v.+)?\.db$")]
    private static partial Regex BackupFileName();

    public string? CreateBackup(BackupKind kind)
    {
        // Lock: o backup diário roda em segundo plano e não pode cruzar com outro backup.
        lock (_lock)
        {
            if (!File.Exists(paths.DatabasePath))
            {
                return null;
            }

            var stopwatch = Stopwatch.StartNew();
            var finalPath = Path.Combine(paths.BackupsDirectory, BuildFileName(kind));
            var tempPath = finalPath + ".tmp";

            try
            {
                Directory.CreateDirectory(paths.BackupsDirectory);
                CopyDatabase(paths.DatabasePath, tempPath);
                VerifyIntegrity(tempPath);
                File.Move(tempPath, finalPath);
            }
            catch
            {
                DeleteQuietly(tempPath);
                throw;
            }

            logger.LogInformation(
                "Backup {Kind} criado em {Path} ({Size} bytes, {Elapsed})",
                kind,
                finalPath,
                new FileInfo(finalPath).Length,
                stopwatch.Elapsed);

            Prune(kind);
            return finalPath;
        }
    }

    public async Task RunDailyIfDueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsDailyDue())
            {
                logger.LogDebug("Backup diário de hoje já existe");
                return;
            }

            // A API de backup do SQLite é síncrona; roda fora da thread de UI.
            await Task.Run(() => CreateBackup(BackupKind.Daily), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha no backup diário; os backups anteriores foram mantidos e a próxima abertura tenta de novo");
        }
    }

    private bool IsDailyDue()
    {
        var today = timeProvider.GetLocalNow().Date;
        return !ListBackups(BackupKind.Daily).Any(b => b.Timestamp.Date >= today);
    }

    private string BuildFileName(BackupKind kind)
    {
        var timestamp = timeProvider.GetLocalNow().ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var suffix = kind == BackupKind.PreMigration ? $"-v{AppVersion()}" : string.Empty;
        var name = $"vibePlayer-{KindNames[kind]}-{timestamp}{suffix}.db";

        // Dois backups no mesmo segundo (raro, mas acontece em testes) não podem se sobrescrever.
        var counter = 1;
        while (File.Exists(Path.Combine(paths.BackupsDirectory, name)))
        {
            name = $"vibePlayer-{KindNames[kind]}-{timestamp}{suffix}-{counter++}.db";
        }

        return name;
    }

    private static void CopyDatabase(string sourcePath, string destinationPath)
    {
        // Pooling desligado para os arquivos serem liberados ao sair daqui (no Windows, uma
        // conexão no pool mantém o arquivo travado e o File.Move falharia).
        using var source = new SqliteConnection($"Data Source={sourcePath};Pooling=False");
        using var destination = new SqliteConnection($"Data Source={destinationPath};Pooling=False");
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);

        // A cópia herda o modo WAL do banco e criaria -wal/-shm ao lado do .tmp, que ficariam
        // órfãos depois do rename. Em modo DELETE o backup é um arquivo único, autocontido.
        using var command = destination.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=DELETE";
        command.ExecuteNonQuery();
    }

    private static void VerifyIntegrity(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check";
        var result = command.ExecuteScalar() as string;

        if (result != "ok")
        {
            throw new InvalidDataException($"Backup não passou na verificação de integridade: {result}");
        }
    }

    private void Prune(BackupKind kind)
    {
        var keep = kind == BackupKind.Daily ? DailyToKeep : PreMigrationToKeep;
        var toDelete = ListBackups(kind).OrderByDescending(b => b.Path, StringComparer.Ordinal).Skip(keep).ToList();

        // Restos de backups interrompidos (.tmp) nunca são válidos.
        var leftovers = Directory.EnumerateFiles(paths.BackupsDirectory, $"vibePlayer-{KindNames[kind]}-*.db.tmp").ToList();

        foreach (var path in toDelete.Select(b => b.Path).Concat(leftovers))
        {
            DeleteQuietly(path);
        }

        if (toDelete.Count + leftovers.Count > 0)
        {
            logger.LogInformation("Backups antigos removidos: {Count}", toDelete.Count + leftovers.Count);
        }
    }

    private IEnumerable<(string Path, DateTime Timestamp)> ListBackups(BackupKind kind)
    {
        if (!Directory.Exists(paths.BackupsDirectory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(paths.BackupsDirectory, "vibePlayer-*.db"))
        {
            var match = BackupFileName().Match(Path.GetFileName(path));
            if (match.Success
                && match.Groups["kind"].Value == KindNames[kind]
                && DateTime.TryParseExact(match.Groups["timestamp"].Value, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
            {
                yield return (path, timestamp);
            }
        }
    }

    private void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Não foi possível apagar {Path}", path);
        }
    }

    private static string AppVersion()
    {
        // Sem o "+<commit>" do MinVer: o nome do arquivo só precisa dizer qual versão atualizou.
        var version = typeof(SqliteBackupService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        return version.Split('+')[0];
    }
}
