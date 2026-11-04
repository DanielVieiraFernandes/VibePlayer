using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VibePlayer.Core;
using VibePlayer.Core.Library;
using VibePlayer.Core.Playback;
using VibePlayer.Core.Persistence;
using VibePlayer.Core.Settings;
using VibePlayer.Core.Transcription;
using VibePlayer.Infrastructure.FileSystem;
using VibePlayer.Infrastructure.Library;
using VibePlayer.Infrastructure.Persistence;
using VibePlayer.Infrastructure.Playback;
using VibePlayer.Infrastructure.Settings;
using VibePlayer.Infrastructure.Transcription;

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

        // Reprodução (spec 0006). O VLC só é carregado quando alguém usa o player.
        services.AddSingleton<VlcRuntime>();
        services.AddSingleton<IMediaPlayer, VlcMediaPlayer>();
        services.AddSingleton<IMediaInfoReader, VlcMediaInfoReader>();
        services.AddSingleton<IThumbnailGenerator, VlcThumbnailGenerator>();
        services.AddSingleton<IThumbnailService, ThumbnailService>();
        services.AddSingleton<ILessonProgressStore, LessonProgressStore>();
        services.AddSingleton<ILessonDurationUpdater, LessonDurationUpdater>();

        // Transcrição (spec 0007). O Whisper e a extração rodam em processo separado.
        services.AddSingleton<ITranscriptStore, TranscriptStore>();
        services.AddSingleton<ISubtitleSource, PhysicalSubtitleSource>();
        services.AddSingleton<IFileHasher, Sha256FileHasher>();
        services.AddSingleton(_ => TranscriptionWorkerCommand.CurrentProcess());
        services.AddSingleton<ITranscriptionWorker, ProcessTranscriptionWorker>();
        services.AddSingleton<IWhisperModelStore>(provider => new WhisperModelStore(
            paths,
            new HttpClient { Timeout = Timeout.InfiniteTimeSpan },
            FreeSpaceOf,
            provider.GetRequiredService<ILogger<WhisperModelStore>>()));
        services.AddSingleton<ITranscriptionService, TranscriptionService>();
        return services;
    }

    private static long FreeSpaceOf(string directory) =>
        new DriveInfo(Path.GetPathRoot(Path.GetFullPath(directory))!).AvailableFreeSpace;
}
