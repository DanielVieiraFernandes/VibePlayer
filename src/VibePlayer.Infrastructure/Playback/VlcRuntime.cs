using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using VibePlayer.Core.Playback;

namespace VibePlayer.Infrastructure.Playback;

/// <summary>
/// Carrega o LibVLC uma vez, na primeira vez que alguém precisa dele (e não na abertura do app,
/// para o app abrir rápido mesmo com o VLC demorando a carregar os plugins).
/// </summary>
/// <remarks>
/// No Linux o VLC vem do sistema e pode não estar instalado: aí <see cref="Availability"/> fica
/// <see cref="PlayerAvailability.Unavailable"/>, o player mostra como instalar e o resto do app
/// segue funcionando.
/// </remarks>
public sealed class VlcRuntime(ILogger<VlcRuntime> logger) : IDisposable
{
    // Opções do VLC (spec 0006):
    // - avcodec-hw=any: decodificação por hardware quando houver (d3d11va, videotoolbox, vaapi);
    // - no-video-title-show: sem o nome do arquivo desenhado por cima do vídeo;
    // - no-sub-autodetect-file: legendas ao lado do vídeo entram com a spec de transcrição;
    // - no-lua / no-stats: recursos que o app não usa (e Lua nem é distribuído).
    private static readonly string[] Options =
    [
        "--avcodec-hw=any",
        "--no-video-title-show",
        "--no-sub-autodetect-file",
        "--no-lua",
        "--no-stats",
    ];

    // Uma instância por processo, nunca liberada (ver Dispose). Estática porque o app pode criar
    // mais de um container (os testes criam vários) e cada LibVLC abandonado, com o callback de
    // log ainda registrado, derrubaria o processo quando o GC coletasse o delegate.
    private static readonly Lock InitLock = new();
    private static LibVLC? s_libVlc;
    private static bool s_initialized;
    private static ILogger? s_logger;

    public PlayerAvailability Availability
    {
        get
        {
            EnsureInitialized();
            return s_libVlc is null ? PlayerAvailability.Unavailable : PlayerAvailability.Available;
        }
    }

    /// <summary>O LibVLC carregado, ou <c>null</c> se o VLC não está disponível.</summary>
    public LibVLC? LibVlc
    {
        get
        {
            EnsureInitialized();
            return s_libVlc;
        }
    }

    /// <summary>
    /// Não libera o LibVLC: a instância é única por processo (ver os campos estáticos) e vive o
    /// tempo do app; o sistema recolhe tudo ao fechar. Liberar exigiria garantir que nenhuma
    /// chamada ao VLC está em andamento em outra thread, sem ganho real num app desktop.
    /// </summary>
    public void Dispose()
    {
    }

    private void EnsureInitialized()
    {
        lock (InitLock)
        {
            if (s_initialized)
            {
                return;
            }

            s_initialized = true;
            s_logger = logger;
            try
            {
                LibVLCSharp.Shared.Core.Initialize();
                s_libVlc = new LibVLC(Options);
                s_libVlc.Log += OnVlcLog;
                logger.LogInformation("VLC carregado: {Version}", s_libVlc.Version);
            }
            catch (Exception ex) when (ex is VLCException or DllNotFoundException or TypeInitializationException or InvalidOperationException)
            {
                logger.LogWarning(ex, "VLC indisponível; a reprodução fica desativada");
            }
        }
    }

    // O VLC é muito verboso: só avisos e erros dele chegam ao log do app (erros como Warning,
    // avisos como Debug, porque o VLC chama de "warning" coisas rotineiras).
    private static void OnVlcLog(object? sender, LogEventArgs e)
    {
        if (e.Level >= LibVLCSharp.Shared.LogLevel.Warning)
        {
            s_logger?.Log(
                e.Level == LibVLCSharp.Shared.LogLevel.Error ? Microsoft.Extensions.Logging.LogLevel.Warning : Microsoft.Extensions.Logging.LogLevel.Debug,
                "VLC ({Module}): {Message}",
                e.Module,
                e.Message);
        }
    }
}
