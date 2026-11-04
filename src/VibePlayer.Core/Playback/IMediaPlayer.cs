namespace VibePlayer.Core.Playback;

public enum PlaybackState
{
    /// <summary>Nada aberto.</summary>
    Idle,

    /// <summary>Abrindo o arquivo (antes do primeiro quadro).</summary>
    Opening,

    Playing,

    Paused,

    /// <summary>Chegou ao fim do vídeo.</summary>
    Ended,

    /// <summary>Não foi possível abrir ou continuar tocando (<see cref="IMediaPlayer.Error"/> diz o motivo).</summary>
    Error,
}

public enum PlaybackError
{
    FileNotFound,
    CannotPlay,
}

/// <summary>Disponibilidade do motor de reprodução (no Linux, o VLC vem do sistema e pode faltar).</summary>
public enum PlayerAvailability
{
    Available,
    Unavailable,
}

/// <summary>
/// Player de vídeo, sem tipos do VLC: o <c>PlayerViewModel</c> e os testes só conhecem isto.
/// </summary>
/// <remarks>
/// Eventos podem chegar em qualquer thread (no VLC, numa thread dele). Quem ouve repassa para a
/// thread de UI e nunca chama o player de dentro do próprio evento: o LibVLC trava com chamadas
/// reentrantes.
/// </remarks>
public interface IMediaPlayer : IDisposable
{
    PlayerAvailability Availability { get; }

    /// <summary>Quadros do vídeo aberto, para a UI desenhar.</summary>
    IVideoFrameSource Video { get; }

    PlaybackState State { get; }

    PlaybackError? Error { get; }

    TimeSpan Position { get; }

    /// <summary>Zero enquanto o arquivo não informou a duração.</summary>
    TimeSpan Duration { get; }

    /// <summary>0 a 100.</summary>
    int Volume { get; set; }

    bool IsMuted { get; set; }

    /// <summary>Velocidade (1 = normal).</summary>
    double Rate { get; set; }

    event EventHandler? StateChanged;

    event EventHandler? PositionChanged;

    event EventHandler? DurationChanged;

    /// <summary>Abre o arquivo começando em <paramref name="startAt"/>; toca já ou fica pausado no primeiro quadro.</summary>
    void Open(string path, TimeSpan startAt, bool autoPlay);

    void Play();

    void Pause();

    void Seek(TimeSpan position);

    /// <summary>Fecha o arquivo atual (volta a <see cref="PlaybackState.Idle"/>).</summary>
    void Stop();
}
