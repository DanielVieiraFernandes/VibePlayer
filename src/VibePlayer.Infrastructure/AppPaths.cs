using VibePlayer.Core;

namespace VibePlayer.Infrastructure;

public sealed class AppPaths : IAppPaths
{
    private const string AppFolderName = "VibePlayer";

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

        // Criadas já na construção porque o Serilog precisa da pasta de logs antes de
        // qualquer outra coisa iniciar. CreateDirectory não faz nada se a pasta já existe.
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ModelsDirectory);
    }

    public string DataDirectory { get; }

    public string LogsDirectory { get; }

    public string ModelsDirectory { get; }

    /// <summary>
    /// Pasta de dados padrão do usuário atual.
    /// </summary>
    /// <remarks>
    /// <see cref="Environment.SpecialFolder.ApplicationData"/> já resolve para o lugar
    /// convencional de cada SO (<c>%APPDATA%</c> no Windows, <c>~/.config</c> no Linux e macOS),
    /// então não há código específico por plataforma aqui.
    /// </remarks>
    public static AppPaths CreateDefault() =>
        new(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppFolderName));
}
