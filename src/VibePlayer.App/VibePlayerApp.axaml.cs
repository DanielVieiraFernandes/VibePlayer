using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VibePlayer.App.Diagnostics;
using VibePlayer.App.Services;
using VibePlayer.App.State;
using VibePlayer.App.ViewModels;
using VibePlayer.App.Views;
using VibePlayer.App.Views.Shell;
using VibePlayer.Core;
using VibePlayer.Core.Persistence;
using VibePlayer.Core.Threading;

namespace VibePlayer.App;

// Chamada VibePlayerApp, e não App, porque uma classe App dentro do namespace VibePlayer.App
// faria "App" ser resolvido como namespace em outros projetos (ex.: nos testes).
public partial class VibePlayerApp : Application
{
    /// <summary>Código de saída quando o banco não pôde ser preparado (ver <see cref="AppStartup"/>).</summary>
    public const int DatabaseErrorExitCode = 2;

    private readonly IServiceProvider? _services;
    private readonly DatabaseInitializationResult _startup = DatabaseInitializationResult.Success();

    /// <summary>Usado apenas pelo previewer de XAML; sem serviços, nenhuma janela real é criada.</summary>
    public VibePlayerApp()
    {
    }

    /// <param name="startup">Resultado de <see cref="AppStartup.Prepare"/>; decide qual janela abre.</param>
    public VibePlayerApp(IServiceProvider services, DatabaseInitializationResult? startup = null)
    {
        _services = services;
        _startup = startup ?? DatabaseInitializationResult.Success();
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (_services is not null)
        {
            // Anexado aqui porque o dispatcher da UI só existe depois que o Avalonia inicializa.
            _services.GetRequiredService<GlobalExceptionHandler>().Attach(Dispatcher.UIThread);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (_startup.Succeeded)
                {
                    desktop.MainWindow = CreateMainWindow();
                    desktop.MainWindow.Opened += (_, _) => StartDailyBackup();
                }
                else
                {
                    var errorWindow = new DatabaseErrorWindow(
                        _startup.BackupPath,
                        _services.GetRequiredService<IAppPaths>().LogsDirectory);
                    errorWindow.Closed += (_, _) => desktop.Shutdown(DatabaseErrorExitCode);
                    desktop.MainWindow = errorWindow;
                }
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Cria a janela principal já ligada ao seu ViewModel. Público para que os testes E2E abram
    /// a mesma janela que o app abre, já que no modo headless não há lifetime de desktop.
    /// </summary>
    public MainWindow CreateMainWindow()
    {
        if (_services is null)
        {
            throw new InvalidOperationException("O app foi criado sem serviços (modo previewer).");
        }

        var viewModel = _services.GetRequiredService<MainWindowViewModel>();
        var window = new MainWindow { DataContext = viewModel };
        _services.GetRequiredService<WindowStateTracker>().Attach(window, viewModel);

        // As notificações (inclusive as de erro do GlobalExceptionHandler) aparecem nesta janela.
        _services.GetRequiredService<NotificationService>().AttachTo(window);
        return window;
    }

    /// <summary>
    /// Depois que a janela abre, para o backup nunca atrasar a abertura. Fica fora de
    /// <see cref="CreateMainWindow"/> para os testes E2E não dispararem backups a cada janela.
    /// </summary>
    private void StartDailyBackup()
    {
        var logger = _services!.GetRequiredService<ILogger<VibePlayerApp>>();
        _services!.GetRequiredService<IBackupService>().RunDailyIfDueAsync().FireAndForget(logger, "BackupDiario");
    }
}
