using Serilog;
using Serilog.Core;
using Serilog.Events;
using VibePlayer.Core;

namespace VibePlayer.App.Diagnostics;

public static class LoggingSetup
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Logger do app, gravando somente em arquivo local.
    /// </summary>
    /// <remarks>
    /// Um arquivo por dia (<c>vibePlayer-20261102.log</c>), mantendo os últimos 14: o bastante para
    /// o usuário anexar o log de um problema recente sem a pasta crescer para sempre.
    /// <c>Microsoft.*</c> e <c>System.*</c> ficam em Warning porque as bibliotecas do .NET
    /// registram muito detalhe interno em Information, o que esconderia os eventos do app.
    /// </remarks>
    public static Logger CreateLogger(IAppPaths paths) =>
        new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(paths.LogsDirectory, "vibePlayer-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: OutputTemplate)
            .CreateLogger();
}
