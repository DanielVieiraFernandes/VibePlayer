using Microsoft.Extensions.DependencyInjection;
using VibePlayer.Core.Playback;

namespace VibePlayer.E2ETests;

/// <summary>
/// Player falso para os E2E: o modo headless não precisa de vídeo de verdade para testar
/// controles, progresso e atalhos, e a CI não toca vídeo. O VLC real é coberto por
/// <see cref="VlcIntegrationTests"/>.
/// </summary>
/// <remarks>
/// Tudo acontece na hora, na thread de quem chama: abrir vai direto para tocando (ou pausado);
/// o teste avança o tempo com <see cref="SimulatePosition"/> e encerra com <see cref="SimulateEnd"/>.
/// </remarks>
public sealed class FakeMediaPlayer : IMediaPlayer, IVideoFrameSource
{
    private static readonly byte[] Frame = Enumerable.Repeat((byte)0x80, 16 * 9 * 4).ToArray();

    public static TimeSpan DefaultDuration { get; } = TimeSpan.FromMinutes(10);

    public List<(string Path, TimeSpan StartAt, bool AutoPlay)> Opened { get; } = [];

    public PlayerAvailability Availability { get; set; } = PlayerAvailability.Available;

    public IVideoFrameSource Video => this;

    public PlaybackState State { get; private set; }

    public PlaybackError? Error { get; private set; }

    public TimeSpan Position { get; private set; }

    public TimeSpan Duration { get; private set; }

    public int Volume { get; set; } = 100;

    public bool IsMuted { get; set; }

    public double Rate { get; set; } = 1;

    public event EventHandler? StateChanged;

    public event EventHandler? PositionChanged;

    public event EventHandler? DurationChanged;

    public event EventHandler? FrameReady;

    public void Open(string path, TimeSpan startAt, bool autoPlay)
    {
        Opened.Add((path, startAt, autoPlay));
        Error = null;
        Position = startAt;
        if (!File.Exists(path))
        {
            Error = PlaybackError.FileNotFound;
            SetState(PlaybackState.Error);
            return;
        }

        SetState(PlaybackState.Opening);
        Duration = DefaultDuration;
        DurationChanged?.Invoke(this, EventArgs.Empty);
        SetState(autoPlay ? PlaybackState.Playing : PlaybackState.Paused);
        if (autoPlay)
        {
            FrameReady?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Play()
    {
        SetState(PlaybackState.Playing);
        FrameReady?.Invoke(this, EventArgs.Empty);
    }

    public void Pause() => SetState(PlaybackState.Paused);

    public void Seek(TimeSpan position) => SimulatePosition(position);

    public void Stop()
    {
        Position = TimeSpan.Zero;
        SetState(PlaybackState.Idle);
    }

    public void SimulatePosition(TimeSpan position)
    {
        Position = position;
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SimulateEnd()
    {
        SimulatePosition(Duration);
        SetState(PlaybackState.Ended);
    }

    public void SimulateError()
    {
        Error = PlaybackError.CannotPlay;
        SetState(PlaybackState.Error);
    }

    public bool ReadLatestFrame(VideoFrameReader reader)
    {
        if (State is PlaybackState.Idle or PlaybackState.Error)
        {
            return false;
        }

        reader(Frame, 16, 9, 16 * 4);
        return true;
    }

    public void Dispose()
    {
    }

    private void SetState(PlaybackState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Duração fixa (ou nenhuma), com atraso opcional para simular leitura lenta.</summary>
public sealed class FakeMediaInfoReader : IMediaInfoReader
{
    public TimeSpan? Duration { get; set; } = TimeSpan.FromMinutes(12);

    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public int Reads;

    public async Task<TimeSpan?> ReadDurationAsync(string path, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Reads);
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        return Duration;
    }
}

/// <summary>Miniatura falsa: grava um JPEG mínimo de verdade (decodificável), ou falha se pedido.</summary>
public sealed class FakeThumbnailGenerator : IThumbnailGenerator
{
    // JPEG 1×1 cinza válido.
    private static readonly byte[] Jpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=");

    public bool Fail { get; set; }

    public List<string> Generated { get; } = [];

    public async Task<bool> GenerateAsync(string videoPath, TimeSpan at, string outputPath, CancellationToken cancellationToken = default)
    {
        if (Fail)
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllBytesAsync(outputPath, Jpeg, cancellationToken);
        lock (Generated)
        {
            Generated.Add(videoPath);
        }

        return true;
    }
}

public static class FakePlaybackServices
{
    /// <summary>Troca o VLC pelos falsos (padrão de todos os E2E; ver TestAppBuilder e TemporaryAppData).</summary>
    public static IServiceCollection UseFakePlayback(
        this IServiceCollection services,
        FakeMediaPlayer? player = null,
        FakeMediaInfoReader? mediaInfo = null,
        FakeThumbnailGenerator? thumbnails = null)
    {
        services.AddSingleton<IMediaPlayer>(player ?? new FakeMediaPlayer());
        services.AddSingleton<IMediaInfoReader>(mediaInfo ?? new FakeMediaInfoReader());
        services.AddSingleton<IThumbnailGenerator>(thumbnails ?? new FakeThumbnailGenerator());
        return services;
    }
}
