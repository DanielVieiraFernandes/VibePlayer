using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using VibePlayer.App.Diagnostics;
using VibePlayer.App.Services;
using VibePlayer.App.ViewModels;
using VibePlayer.App.Views;

namespace VibePlayer.App;

// Chamada VibePlayerApp, e não App, porque uma classe App dentro do namespace VibePlayer.App
// faria "App" ser resolvido como namespace em outros projetos (ex.: nos testes).
public partial class VibePlayerApp : Application
{
    private readonly IServiceProvider? _services;

    /// <summary>Usado apenas pelo previewer de XAML; sem serviços, nenhuma janela real é criada.</summary>
    public VibePlayerApp()
    {
    }

    public VibePlayerApp(IServiceProvider services)
    {
        _services = services;
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
                desktop.MainWindow = CreateMainWindow();
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

        var window = new MainWindow
        {
            DataContext = _services.GetRequiredService<MainWindowViewModel>(),
        };

        // As notificações (inclusive as de erro do GlobalExceptionHandler) aparecem nesta janela.
        _services.GetRequiredService<NotificationService>().AttachTo(window);
        return window;
    }
}
