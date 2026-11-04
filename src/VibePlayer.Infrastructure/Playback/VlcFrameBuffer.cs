using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using VibePlayer.Core.Playback;

namespace VibePlayer.Infrastructure.Playback;

/// <summary>
/// Recebe os quadros decodificados pelo VLC em memória (callbacks de vídeo) para o app desenhar.
/// </summary>
/// <remarks>
/// Fluxo, com as callbacks rodando em threads do VLC:
/// <list type="number">
/// <item><c>Format</c>: o VLC informa o tamanho do vídeo; respondemos RV32 (BGRA) e o tamanho de
/// saída (o original, limitado a <see cref="MaxWidth"/>) e alocamos dois buffers.</item>
/// <item><c>Lock</c>: o VLC decodifica o próximo quadro no buffer de trás.</item>
/// <item><c>Display</c>: o quadro está pronto; trocamos trás e frente sob lock e avisamos
/// <see cref="FrameReady"/>. Quem desenha lê o da frente com <see cref="ReadLatestFrame"/>.</item>
/// </list>
/// Dois buffers para o decodificador nunca escrever no quadro que a UI está copiando. Os delegates
/// ficam em campos: o VLC guarda ponteiros para eles, e o GC não pode coletá-los.
/// </remarks>
public sealed unsafe class VlcFrameBuffer : IVideoFrameSource, IDisposable
{
    /// <summary>Acima disso o VLC já entrega reduzido: 4K custaria 4 vezes a cópia sem ganho visível.</summary>
    public const int MaxWidth = 1920;

    private const int BytesPerPixel = 4;

    private readonly Lock _lock = new();
    private readonly MediaPlayer.LibVLCVideoFormatCb _format;
    private readonly MediaPlayer.LibVLCVideoCleanupCb _cleanup;
    private readonly MediaPlayer.LibVLCVideoLockCb _lockFrame;
    private readonly MediaPlayer.LibVLCVideoDisplayCb _display;

    private IntPtr _back;
    private IntPtr _front;
    private int _width;
    private int _height;
    private int _stride;
    private bool _hasFrame;

    // O formato informado pelo VLC traz a altura com o preenchimento do decodificador (ex.: 192 ou
    // 194 linhas para um vídeo de 180). O tamanho visível vem de MediaPlayer.Size, que não pode ser
    // chamado de dentro das callbacks: é consultado na primeira leitura depois de cada formato.
    private readonly MediaPlayer _player;
    private double _scale = 1;
    private int _visibleWidth;
    private int _visibleHeight;
    private bool _visibleKnown;

    public VlcFrameBuffer(MediaPlayer player)
    {
        _player = player;
        _format = OnFormat;
        _cleanup = OnCleanup;
        _lockFrame = OnLock;
        _display = OnDisplay;
        player.SetVideoFormatCallbacks(_format, _cleanup);
        player.SetVideoCallbacks(_lockFrame, null, _display);
    }

    public event EventHandler? FrameReady;

    public bool ReadLatestFrame(VideoFrameReader reader)
    {
        // Fora do lock: o VLC pode estar parado no OnDisplay esperando o lock, e Size precisaria
        // dele (trava).
        if (!Volatile.Read(ref _visibleKnown))
        {
            ResolveVisibleSize();
        }

        lock (_lock)
        {
            if (!_hasFrame || _front == IntPtr.Zero)
            {
                return false;
            }

            var width = Math.Min(_visibleWidth, _width);
            var height = Math.Min(_visibleHeight, _height);
            reader(new ReadOnlySpan<byte>((void*)_front, _stride * height), width, height, _stride);
            return true;
        }
    }

    private void ResolveVisibleSize()
    {
        uint width = 0;
        uint height = 0;
        var known = _player.Size(0, ref width, ref height) && width > 0 && height > 0;

        lock (_lock)
        {
            _visibleWidth = known ? (int)Math.Round(width * _scale) : _width;
            _visibleHeight = known ? (int)Math.Round(height * _scale) : _height;
            _visibleKnown = known;
        }
    }

    /// <summary>Esquece o último quadro (vídeo fechado), para a UI não mostrar a aula anterior.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _hasFrame = false;
        }

        FrameReady?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => OnCleanup(IntPtr.Zero);

    private uint OnFormat(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        // "RV32" = BGRA em memória (little-endian), o Bgra8888 do Avalonia e do SkiaSharp.
        Marshal.Copy("RV32"u8.ToArray(), 0, chroma, 4);

        var scale = 1.0;
        if (width > MaxWidth)
        {
            scale = MaxWidth / (double)width;
            height = (uint)Math.Round(height * scale);
            width = MaxWidth;
        }

        // Linha e altura alinhadas a 32: os conversores de cor do VLC escrevem em blocos e
        // passariam do fim de um buffer com o tamanho exato (o processo cai, sem exceção).
        var stride = Align((int)width * BytesPerPixel, 32);
        var allocatedLines = Align((int)height, 32);

        lock (_lock)
        {
            FreeBuffers();
            _width = (int)width;
            _height = (int)height;
            _scale = scale;
            _visibleWidth = _width;
            _visibleHeight = _height;
            _visibleKnown = false;
            _stride = stride;
            _back = Marshal.AllocHGlobal(stride * allocatedLines);
            _front = Marshal.AllocHGlobal(stride * allocatedLines);
            _hasFrame = false;
        }

        pitches = (uint)stride;
        lines = (uint)allocatedLines;
        return 1;
    }

    private void OnCleanup(ref IntPtr opaque) => OnCleanup(opaque);

    private void OnCleanup(IntPtr opaque)
    {
        lock (_lock)
        {
            FreeBuffers();
            _hasFrame = false;
        }
    }

    private IntPtr OnLock(IntPtr opaque, IntPtr planes)
    {
        // O VLC decodifica direto no buffer de trás; a troca só acontece no Display.
        Marshal.WriteIntPtr(planes, _back);
        return IntPtr.Zero;
    }

    private void OnDisplay(IntPtr opaque, IntPtr picture)
    {
        lock (_lock)
        {
            (_back, _front) = (_front, _back);
            _hasFrame = true;
        }

        FrameReady?.Invoke(this, EventArgs.Empty);
    }

    private static int Align(int value, int alignment) => (value + alignment - 1) / alignment * alignment;

    private void FreeBuffers()
    {
        if (_back != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_back);
            _back = IntPtr.Zero;
        }

        if (_front != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_front);
            _front = IntPtr.Zero;
        }
    }
}
