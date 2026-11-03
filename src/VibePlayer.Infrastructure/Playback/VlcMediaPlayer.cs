using System.Globalization;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using VibePlayer.Core.Playback;

namespace VibePlayer.Infrastructure.Playback;

/// <summary><see cref="IMediaPlayer"/> sobre o LibVLCSharp.</summary>
/// <remarks>
/// Regra de threads: os eventos do VLC chegam em threads dele e aqui só atualizam o estado e
/// repassam o aviso. Nenhum método do <see cref="MediaPlayer"/> é chamado de dentro de um evento
/// dele, porque o LibVLC trava com chamadas reentrantes; quem reage (ex.: tocar a próxima aula)
/// faz isso pela thread de UI.
/// </remarks>
public sealed class VlcMediaPlayer(VlcRuntime runtime, ILogger<VlcMediaPlayer> logger) : IMediaPlayer
{
    private readonly FrameSource _video = new();
    private MediaPlayer? _player;
    private VlcFrameBuffer? _frames;
    private Media? _media;
    private int _volume = 100;
    private bool _muted;
    private double _rate = 1;

    public event EventHandler? StateChanged;

    public event EventHandler? PositionChanged;

    public event EventHandler? DurationChanged;

    public PlayerAvailability Availability => runtime.Availability;

    /// <summary>Fixa desde a criação: a UI se inscreve antes de o VLC (e o buffer) existirem.</summary>
    public IVideoFrameSource Video => _video;

    public PlaybackState State { get; private set; } = PlaybackState.Idle;

    public PlaybackError? Error { get; private set; }

    public TimeSpan Position { get; private set; }

    public TimeSpan Duration { get; private set; }

    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            _player?.Volume = _volume;
        }
    }

    public bool IsMuted
    {
        get => _muted;
        set
        {
            _muted = value;
            _player?.Mute = value;
        }
    }

    public double Rate
    {
        get => _rate;
        set
        {
            _rate = value;
            _player?.SetRate((float)value);
        }
    }

    public void Open(string path, TimeSpan startAt, bool autoPlay)
    {
        Stop();
        Error = null;
        Position = startAt;
        Duration = TimeSpan.Zero;

        if (!File.Exists(path))
        {
            Fail(PlaybackError.FileNotFound);
            return;
        }

        if (EnsurePlayer() is not { } player)
        {
            return;
        }

        _media = new Media(runtime.LibVlc!, new Uri(path));

        // start-time na mídia, e não um seek depois de abrir: assim não toca meio segundo do
        // começo antes de pular. start-paused abre parado no quadro, sem som (reabertura do app).
        _media.AddOption(":start-time=" + startAt.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
        if (!autoPlay)
        {
            _media.AddOption(":start-paused");
        }

        player.Media = _media;
        SetState(PlaybackState.Opening);
        player.Play();
        logger.LogDebug("Abrindo {Path} em {Start}", path, startAt);
    }

    public void Play()
    {
        if (_player is null || _media is null)
        {
            return;
        }

        if (State == PlaybackState.Ended)
        {
            // No fim, o VLC precisa recarregar a mídia para tocar de novo.
            _player.Stop();
            _player.Play();
            return;
        }

        _player.SetPause(false);
    }

    public void Pause() => _player?.SetPause(true);

    public void Seek(TimeSpan position)
    {
        if (_player is null || _media is null)
        {
            return;
        }

        var target = Duration > TimeSpan.Zero ? TimeSpan.FromTicks(Math.Clamp(position.Ticks, 0, Duration.Ticks)) : position;
        _player.Time = (long)target.TotalMilliseconds;
        Position = target;
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        if (_player is not null)
        {
            _player.Stop();
            _frames?.Clear();
        }

        _media?.Dispose();
        _media = null;
        SetState(PlaybackState.Idle);
    }

    public void Dispose()
    {
        Stop();
        _player?.Dispose();
        _frames?.Dispose();
    }

    private MediaPlayer? EnsurePlayer()
    {
        if (_player is not null)
        {
            return _player;
        }

        if (runtime.LibVlc is not { } libVlc)
        {
            Fail(PlaybackError.CannotPlay);
            return null;
        }

        _player = new MediaPlayer(libVlc)
        {
            // O app trata teclado e mouse; o VLC não deve reagir a eles.
            EnableKeyInput = false,
            EnableMouseInput = false,
        };
        _frames = new VlcFrameBuffer(_player);
        _video.Attach(_frames);

        _player.Opening += (_, _) => SetState(PlaybackState.Opening);
        _player.Playing += (_, _) => OnPlaying();
        _player.Paused += (_, _) => SetState(PlaybackState.Paused);
        _player.EndReached += (_, _) => SetState(PlaybackState.Ended);
        _player.EncounteredError += (_, _) => Fail(PlaybackError.CannotPlay);
        _player.TimeChanged += (_, e) =>
        {
            Position = TimeSpan.FromMilliseconds(e.Time);
            PositionChanged?.Invoke(this, EventArgs.Empty);
        };
        _player.LengthChanged += (_, e) =>
        {
            Duration = TimeSpan.FromMilliseconds(e.Length);
            DurationChanged?.Invoke(this, EventArgs.Empty);
        };

        return _player;
    }

    private void OnPlaying()
    {
        SetState(PlaybackState.Playing);

        // Volume, mudo e velocidade só "pegam" depois que a saída de áudio existe, então são
        // reaplicados ao começar a tocar. Fora do evento: qualquer chamada ao LibVLC de dentro de
        // um evento dele derruba o processo (foi o que aconteceu na primeira versão).
        var player = _player!;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            player.Volume = _volume;
            player.Mute = _muted;
            player.SetRate((float)_rate);
        });
    }

    private void Fail(PlaybackError error)
    {
        Error = error;
        logger.LogError("Falha na reprodução: {Error}", error);
        SetState(PlaybackState.Error);
    }

    private void SetState(PlaybackState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Repassa os quadros do buffer do VLC, que só é criado na primeira abertura.</summary>
    private sealed class FrameSource : IVideoFrameSource
    {
        private VlcFrameBuffer? _buffer;

        public event EventHandler? FrameReady;

        public void Attach(VlcFrameBuffer buffer)
        {
            _buffer = buffer;
            buffer.FrameReady += (_, _) => FrameReady?.Invoke(this, EventArgs.Empty);
        }

        public bool ReadLatestFrame(VideoFrameReader reader) => _buffer?.ReadLatestFrame(reader) ?? false;
    }
}
