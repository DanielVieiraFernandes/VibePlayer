using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VibePlayer.Core;
using VibePlayer.Core.Library;
using VibePlayer.Core.Persistence;
using VibePlayer.Core.Settings;
using VibePlayer.Infrastructure.FileSystem;
using VibePlayer.Infrastructure.Library;
using VibePlayer.Infrastructure.Persistence;
using VibePlayer.Infrastructure.Settings;

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
        services.AddSingleton(TimeProvider.System);

        // Factory (singleton) e não contexto registrado direto: ver VibePlayerDbContext.
        services.AddDbContextFactory<VibePlayerDbContext>(options =>
            options.UseSqlite(VibePlayerDbContext.CreateConnectionString(paths.DatabasePath)));

        services.AddSingleton<IBackupService, SqliteBackupService>();
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<ISettingsStore, SqliteSettingsStore>();

        services.AddSingleton<IFileSystemReader, PhysicalFileSystemReader>();
        services.AddSingleton<ICourseLibrary, CourseLibrary>();
        services.AddSingleton<LibraryFolderValidator>();
        services.AddSingleton<VideoSelectionValidator>();
        return services;
    }
}
