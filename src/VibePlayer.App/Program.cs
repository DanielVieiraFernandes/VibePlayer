using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Serilog;
using VibePlayer.App.Diagnostics;
using VibePlayer.Core.Persistence;
using VibePlayer.Core.Transcription;
using VibePlayer.Infrastructure;
using VibePlayer.Infrastructure.Transcription.Worker;

namespace VibePlayer.App;

internal static class Program
{
    /// <remarks>
    /// Ordem de inicialização importa: o Serilog é configurado antes de tudo para que qualquer
    /// falha durante o bootstrap (container, Avalonia) já fique registrada. Só a criação da
    /// pasta de dados acontece antes — sem ela não há onde gravar o log. O banco é preparado
    /// antes do Avalonia (ver <see cref="AppStartup"/>).
    /// </remarks>
    [STAThread]
    public static int Main(string[] args)
    {
        // Antes de tudo: o processo de transcrição não usa log em arquivo, banco nem interface.
        if (args is [WorkerProtocol.Argument])
        {
            return WorkerEntryPoint.Main();
        }

        var paths = AppPaths.CreateDefault();
        Log.Logger = LoggingSetup.CreateLogger(paths);
        AppDomain.CurrentDomain.UnhandledException += OnFatalException;

        try
        {
            LogStartup();
            using var services = AppComposition.BuildServices(paths, Log.Logger);

            // Banco pronto antes da UI; se falhar, o app abre só a janela de erro e encerra.
            var startup = AppStartup.Prepare(services);
            return BuildAvaloniaApp(services, startup).StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Falha ao iniciar o VibePlayer");
            return 1;
        }
        finally
        {
            Log.Information("VibePlayer encerrado");
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp(IServiceProvider services, DatabaseInitializationResult startup) =>
        AppBuilder.Configure(() => new VibePlayerApp(services, startup))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// Usado apenas pelo previewer de XAML das IDEs, que procura este método sem parâmetros.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<VibePlayerApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void LogStartup()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "desconhecida";

        Log.Information(
            "Iniciando VibePlayer {Version} em {OS} {Architecture} ({Runtime})",
            version,
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.FrameworkDescription);
    }

    /// <summary>
    /// Último recurso: exceção numa thread que o .NET não permite recuperar.
    /// </summary>
    /// <remarks>
    /// Diferente da UI e das tasks (ver <see cref="GlobalExceptionHandler"/>), aqui não existe
    /// "Handled": o processo vai encerrar ao sair deste método. O CloseAndFlush garante que o
    /// registro chegue ao disco antes disso, para o problema poder ser investigado.
    /// </remarks>
    private static void OnFatalException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.ExceptionObject as Exception, "Exceção irrecuperável; o VibePlayer será encerrado");
        Log.CloseAndFlush();
    }
}
