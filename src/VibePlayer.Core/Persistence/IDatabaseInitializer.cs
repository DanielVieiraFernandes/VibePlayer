namespace VibePlayer.Core.Persistence;

/// <summary>Deixa o banco pronto e atualizado antes de qualquer tela usá-lo.</summary>
public interface IDatabaseInitializer
{
    /// <summary>
    /// Cria o banco ou aplica as migrations pendentes. Síncrono de propósito: roda uma vez, na
    /// inicialização, antes de a UI existir. Nunca lança; falhas viram o resultado.
    /// </summary>
    DatabaseInitializationResult Initialize();
}

/// <param name="BackupPath">
/// Backup feito antes da atualização, quando houve um; na falha, o banco foi restaurado a partir dele.
/// </param>
public sealed record DatabaseInitializationResult(bool Succeeded, string? BackupPath)
{
    public static DatabaseInitializationResult Success(string? backupPath = null) => new(true, backupPath);

    public static DatabaseInitializationResult Failure(string? backupPath) => new(false, backupPath);
}
