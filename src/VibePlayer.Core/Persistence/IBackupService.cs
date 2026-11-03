namespace VibePlayer.Core.Persistence;

public enum BackupKind
{
    /// <summary>Um por dia, na primeira abertura; os 7 mais recentes são mantidos.</summary>
    Daily,

    /// <summary>Antes de aplicar migrations; os 3 mais recentes são mantidos.</summary>
    PreMigration,
}

/// <summary>Backups do banco na pasta <c>backups/</c> dos dados do app.</summary>
public interface IBackupService
{
    /// <summary>
    /// Cria um backup verificado e aplica a retenção do tipo. Lança se não conseguir: quem chama
    /// decide se a falha impede o que vem depois (atualização do banco) ou só vai para o log.
    /// </summary>
    /// <returns>Caminho do backup, ou <c>null</c> se ainda não existe banco para copiar.</returns>
    string? CreateBackup(BackupKind kind);

    /// <summary>
    /// Cria o backup diário se ainda não houver um de hoje. Nunca lança: falhas vão para o log e
    /// a próxima abertura tenta de novo.
    /// </summary>
    Task RunDailyIfDueAsync(CancellationToken cancellationToken = default);
}
