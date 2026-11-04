using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace VibePlayer.Infrastructure.Transcription.Worker;

/// <summary>
/// Extrai o áudio com o próprio VLC, sem ffmpeg, na saída de stream (transcode + smem).
/// </summary>
/// <remarks>
/// Cada detalhe da cadeia veio de uma falha medida no protótipo (design da spec 0007):
/// <list type="bullet">
/// <item><c>amem</c> não serve: segue o relógio de reprodução (1x), e com <c>rate</c> maior o VLC
/// comprime o áudio ou deixa de entregá-lo.</item>
/// <item><c>--audio-resampler=speex_resampler</c> é obrigatório e precisa ser opção da instância.
/// Sem ele, o VLC usa o "ugly", que perde o resto de cada bloco: 1,17 s a cada 20 min em AAC.</item>
/// <item><c>smem</c>, e não WAV em arquivo: o WAV não guarda o PTS, e um buraco no áudio deslocaria
/// tudo que vem depois (ver <see cref="PtsGapFiller"/>).</item>
/// <item><c>no-time-sync</c>: sem isso o smem espera o tempo real. "time-sync=false" não funciona.</item>
/// </list>
///
/// Roda no processo de transcrição, com uma instância própria do LibVLC: as opções acima não podem
/// atingir o player, e uma falha nativa com um arquivo ruim não pode derrubar o app.
///
/// Regra de threads do VLC: os callbacks só copiam memória. Nenhum deles chama o LibVLC.
/// </remarks>
public sealed class VlcAudioExtractor : IAudioExtractor
{
    private const int ParseTimeoutMs = 15_000;

    // A extração roda a centenas de vezes o tempo real: um minuto sem nenhuma amostra nova
    // significa VLC travado, e não arquivo grande.
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);

    private static readonly string[] Options =
    [
        "--no-video",
        "--no-sub-autodetect-file",
        "--no-lua",
        "--no-stats",
        "--audio-resampler=speex_resampler",
    ];

    private static readonly Lock InitLock = new();

    // Uma instância, nunca liberada, pelo mesmo motivo do VlcRuntime: o callback de log vive tanto
    // quanto ela. Estático também porque os testes criam vários extratores no mesmo processo.
    private static LibVLC? s_libVlc;
    private static volatile bool s_lowQualityResampler;

    public async Task<AudioExtractionResult> ExtractAsync(string path, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var libVlc = GetLibVlc();
        using var media = new Media(libVlc, new Uri(path));
        var parsed = await media.Parse(MediaParseOptions.ParseLocal, ParseTimeoutMs, cancellationToken).ConfigureAwait(false);

        // O código depois do Parse continuaria na thread do VLC (ver VlcMediaInfoReader).
        await Task.Yield();

        // Um arquivo corrompido e um sem áudio terminam igual, com zero blocos, e o VLC não
        // dispara erro em nenhum dos dois. Só as faixas lidas aqui separam os dois casos.
        var tracks = media.Tracks;
        if (parsed != MediaParsedStatus.Done || tracks.Length == 0)
        {
            return new AudioExtractionResult(AudioExtractionStatus.Unreadable, default);
        }

        if (!tracks.Any(track => track.TrackType == TrackType.Audio))
        {
            return new AudioExtractionResult(AudioExtractionStatus.NoAudio, default);
        }

        using var session = new Session(media.Duration, progress);
        media.AddOption(":no-video");
        media.AddOption(":sout-keep");

        // Por padrão o smem recebe todas as faixas de áudio no mesmo callback, emendadas. Vale a
        // primeira, como no player.
        media.AddOption(":no-sout-all");
        media.AddOption(session.StreamOutput);

        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var player = new MediaPlayer(media);
        player.EndReached += (_, _) => finished.TrySetResult();
        player.EncounteredError += (_, _) => finished.TrySetResult();
        player.Play();

        try
        {
            await WaitAsync(session, finished.Task, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Stop no evento do VLC trava (regra de threads), e só depois dele é seguro soltar o
            // buffer usado pelos callbacks, no Dispose da sessão.
            await Task.Yield();
            player.Stop();
        }

        return session.Timeline.Count == 0
            ? new AudioExtractionResult(AudioExtractionStatus.Unreadable, default)
            : new AudioExtractionResult(AudioExtractionStatus.Completed, session.Timeline.Samples, s_lowQualityResampler);
    }

    private static async Task WaitAsync(Session session, Task finished, CancellationToken cancellationToken)
    {
        var lastCount = -1;
        while (!finished.IsCompleted)
        {
            await Task.WhenAny(finished, Task.Delay(StallTimeout, cancellationToken)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var count = session.Timeline.Count;
            if (!finished.IsCompleted && count == lastCount)
            {
                throw new TimeoutException("O VLC parou de entregar áudio.");
            }

            lastCount = count;
        }
    }

    private static LibVLC GetLibVlc()
    {
        lock (InitLock)
        {
            if (s_libVlc is null)
            {
                LibVLCSharp.Shared.Core.Initialize();
                s_libVlc = new LibVLC(Options);
                s_libVlc.Log += OnVlcLog;
            }

            return s_libVlc;
        }
    }

    // O VLC cai no "ugly" sem avisar quando o speex_resampler não existe (ex.: alguns pacotes de
    // Linux); o único sinal é esta linha do log dele.
    private static void OnVlcLog(object? sender, LogEventArgs e)
    {
        if (e.Message.Contains("resampler module \"ugly\"", StringComparison.Ordinal))
        {
            s_lowQualityResampler = true;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnPreRender(nint data, byte** buffer, nuint size) =>
        Session.FromHandle(data).Prepare(buffer, size);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnPostRender(nint data, byte* buffer, int channels, int rate, int samples, int bits, nuint size, long pts) =>
        Session.FromHandle(data).Receive(buffer, samples, pts);

    /// <summary>Estado de uma extração, que chega aos callbacks estáticos pelo <c>audio-data</c> do smem.</summary>
    private sealed unsafe class Session : IDisposable
    {
        private readonly IProgress<double>? _progress;
        private readonly long _expectedSamples;
        private GCHandle _handle;
        private void* _buffer;
        private nuint _bufferSize;
        private int _lastPercent;

        public Session(long durationMs, IProgress<double>? progress)
        {
            _progress = progress;
            _expectedSamples = durationMs * PtsGapFiller.SampleRate / 1000;
            Timeline = new PtsGapFiller((int)Math.Min(_expectedSamples, Array.MaxLength));
            _handle = GCHandle.Alloc(this);
        }

        public PtsGapFiller Timeline { get; }

        public string StreamOutput
        {
            get
            {
                delegate* unmanaged[Cdecl]<nint, byte**, nuint, void> preRender = &OnPreRender;
                delegate* unmanaged[Cdecl]<nint, byte*, int, int, int, int, nuint, long, void> postRender = &OnPostRender;
                var data = GCHandle.ToIntPtr(_handle).ToString(CultureInfo.InvariantCulture);
                return ":sout=#transcode{vcodec=none,scodec=none,acodec=s16l,channels=1,samplerate=16000}" +
                    $":smem{{audio-prerender-callback={(nint)preRender},audio-postrender-callback={(nint)postRender},audio-data={data},no-time-sync}}";
            }
        }

        public static Session FromHandle(nint data) => (Session)GCHandle.FromIntPtr(data).Target!;

        public void Prepare(byte** buffer, nuint size)
        {
            if (size > _bufferSize)
            {
                _buffer = NativeMemory.Realloc(_buffer, size);
                _bufferSize = size;
            }

            *buffer = (byte*)_buffer;
        }

        public void Receive(byte* buffer, int samples, long pts)
        {
            Timeline.Append(new ReadOnlySpan<short>(buffer, samples), pts);
            if (_progress is not null && _expectedSamples > 0)
            {
                var percent = (int)(Timeline.Count * 100 / _expectedSamples);
                if (percent != _lastPercent)
                {
                    _lastPercent = percent;
                    _progress.Report(Math.Min(percent / 100d, 1));
                }
            }
        }

        public void Dispose()
        {
            NativeMemory.Free(_buffer);
            _buffer = null;
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
        }
    }
}
