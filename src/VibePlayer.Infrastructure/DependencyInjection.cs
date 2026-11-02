using Microsoft.Extensions.DependencyInjection;
using VibePlayer.Core;

namespace VibePlayer.Infrastructure;

public static class DependencyInjection
{
    /// <param name="paths">
    /// Recebido pronto, e não criado aqui, porque o Serilog é configurado antes do container
    /// e já precisou dele para saber onde gravar os logs.
    /// </param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IAppPaths paths)
    {
        services.AddSingleton(paths);
        return services;
    }
}
