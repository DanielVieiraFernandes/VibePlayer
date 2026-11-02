using Microsoft.Extensions.DependencyInjection;
using VibePlayer.App.State;
using VibePlayer.Core.Persistence;

namespace VibePlayer.App;

/// <summary>
/// O que precisa estar pronto antes da primeira janela. Compartilhado por <c>Program</c> e pelos
/// testes E2E, para os testes subirem o app do mesmo jeito que a produção.
/// </summary>
public static class AppStartup
{
    /// <remarks>
    /// Roda antes do Avalonia iniciar, numa thread sem SynchronizationContext, por isso a espera
    /// síncrona por <see cref="WindowStateTracker.LoadAsync"/> não tem como travar.
    /// </remarks>
    public static DatabaseInitializationResult Prepare(IServiceProvider services)
    {
        var result = services.GetRequiredService<IDatabaseInitializer>().Initialize();
        if (result.Succeeded)
        {
            services.GetRequiredService<WindowStateTracker>().LoadAsync().GetAwaiter().GetResult();
        }

        return result;
    }
}
