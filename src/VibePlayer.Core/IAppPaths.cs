namespace VibePlayer.Core;

/// <summary>
/// Pastas onde o app guarda seus próprios dados (banco, logs, modelos do Whisper).
/// Nunca aponta para a pasta dos cursos: o app não escreve nada ao lado dos vídeos do usuário.
/// </summary>
public interface IAppPaths
{
    /// <summary>Raiz dos dados do app no perfil do usuário.</summary>
    string DataDirectory { get; }

    string LogsDirectory { get; }

    /// <summary>Banco SQLite (<c>vibePlayer.db</c>).</summary>
    string DatabasePath { get; }

    /// <summary>Backups automáticos do banco.</summary>
    string BackupsDirectory { get; }

    /// <summary>Miniaturas das aulas (cache regenerável, fora dos backups).</summary>
    string ThumbnailsDirectory { get; }

    /// <summary>Modelos do Whisper, baixados na primeira transcrição.</summary>
    string ModelsDirectory { get; }
}
