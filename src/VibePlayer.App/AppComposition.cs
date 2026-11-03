using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using VibePlayer.App.Diagnostics;
using VibePlayer.App.Services;
using VibePlayer.App.State;
using VibePlayer.App.ViewModels;
using VibePlayer.Core;
using VibePlayer.Infrastructure;

namespace VibePlayer.App;

/// <summary>
/// Composition root: o único lugar que conhece todas as implementações concretas.
/// </summary>
public static class AppComposition
{
    /// <param name="logger">
    /// Logger Serilog já configurado. Recebido de fora para que os testes E2E injetem um
    /// logger em memória e verifiquem o que foi registrado, sem gravar arquivos.
    /// </param>
    /// <param name="overrides">
    /// Só para testes: troca registros depois dos de produção (ex.: um relógio falso).
    /// </param>
    public static ServiceProvider BuildServices(
        IAppPaths paths,
        Serilog.ILogger logger,
        Action<IServiceCollection>? overrides = null)
    {
        var services = new ServiceCollection();

        // As classes dependem de ILogger<T> (Microsoft.Extensions.Logging) e o Serilog fica só
        // aqui, como provedor. Assim Core e Infrastructure não conhecem o Serilog.
        // dispose: false porque quem criou o logger (Program ou o teste) é quem o encerra.
        services.AddLogging(builder => builder.ClearProviders().AddSerilog(logger, dispose: false));

        services.AddInfrastructure(paths);
        services.AddSingleton<NotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
        services.AddSingleton<GlobalExceptionHandler>();
        services.AddSingleton<WindowStateTracker>();

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<PlayerViewModel>();
        services.AddTransient<AssistantViewModel>();

        overrides?.Invoke(services);

        // ValidateOnBuild acusa dependências não registradas na inicialização,
        // em vez de só quando a tela que as usa for aberta.
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
