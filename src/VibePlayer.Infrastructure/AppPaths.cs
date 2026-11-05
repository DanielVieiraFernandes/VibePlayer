using VibePlayer.Core;

namespace VibePlayer.Infrastructure;

public sealed class AppPaths : IAppPaths
{
    private const string AppFolderName = "VibePlayer";

    /// <summary>
    /// Pasta de dados alternativa, para testar o app de verdade sem tocar na biblioteca real
    /// (teste manual da spec 0008). Quem não define a variável não nota diferença.
    /// </summary>
    public const string DataDirectoryVariable = "VIBEPLAYER_DATA_DIR";

    /// <param name="dataDirectory">
    /// Raiz dos dados. Em produção use <see cref="CreateDefault"/>; os testes passam uma pasta
    /// temporária para não tocar nos dados reais do usuário.
    /// </param>
    public AppPaths(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        DataDirectory = dataDirectory;
        LogsDirectory = Path.Combine(dataDirectory, "logs");
        ModelsDirectory = Path.Combine(dataDirectory, "models");
        BackupsDirectory = Path.Combine(dataDirectory, "backups");
        ThumbnailsDirectory = Path.Combine(dataDirectory, "thumbnails");
        DatabasePath = Path.Combine(dataDirectory, "vibePlayer.db");

        // Criadas já na construção porque o Serilog precisa da pasta de logs antes de
        // qualquer outra coisa iniciar. CreateDirectory não faz nada se a pasta já existe.
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ModelsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(ThumbnailsDirectory);
    }

    public string DataDirectory { get; }

    public string LogsDirectory { get; }

    public string ModelsDirectory { get; }

    public string DatabasePath { get; }

    public string BackupsDirectory { get; }

    public string ThumbnailsDirectory { get; }

    /// <summary>
    /// Pasta de dados padrão do usuário atual.
    /// </summary>
    /// <remarks>
    /// <see cref="Environment.SpecialFolder.ApplicationData"/> já resolve para o lugar
    /// convencional de cada SO (<c>%APPDATA%</c> no Windows, <c>~/.config</c> no Linux e macOS),
    /// então não há código específico por plataforma aqui.
    /// </remarks>
    public static AppPaths CreateDefault() =>
        new(Environment.GetEnvironmentVariable(DataDirectoryVariable) is { Length: > 0 } custom
            ? custom
            : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppFolderName));
}
