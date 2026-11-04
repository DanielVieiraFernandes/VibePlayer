using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VibePlayer.Core.Playback;

namespace VibePlayer.App.Controls;

/// <summary>
/// Desenha os quadros entregues pelo player (spec 0006, "Renderização do vídeo"), centralizados e
/// sem distorcer (faixas nas laterais ou em cima e embaixo quando a proporção não bate).
/// </summary>
/// <remarks>
/// Os avisos de quadro novo chegam na thread do decodificador, e pode haver vários antes de a UI
/// conseguir desenhar. Eles são agrupados: só uma cópia agendada por vez, sempre do quadro mais
/// recente. Assim a UI nunca fica para trás acumulando cópias de quadros que já passaram.
/// </remarks>
public sealed class VideoView : Control
{
    public static readonly StyledProperty<IVideoFrameSource?> SourceProperty =
        AvaloniaProperty.Register<VideoView, IVideoFrameSource?>(nameof(Source));

    private WriteableBitmap? _bitmap;
    private int _updateScheduled;

    static VideoView()
    {
        AffectsRender<VideoView>(SourceProperty);
    }

    public IVideoFrameSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Se já há um quadro desenhado (os testes usam para conferir que o vídeo aparece).</summary>
    public bool HasFrame => _bitmap is not null;

    public override void Render(DrawingContext context)
    {
        if (_bitmap is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var size = _bitmap.Size;
        var scale = Math.Min(Bounds.Width / size.Width, Bounds.Height / size.Height);
        var target = new Rect(
            (Bounds.Width - (size.Width * scale)) / 2,
            (Bounds.Height - (size.Height * scale)) / 2,
            size.Width * scale,
            size.Height * scale);
        context.DrawImage(_bitmap, new Rect(size), target);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            if (change.OldValue is IVideoFrameSource old)
            {
                old.FrameReady -= OnFrameReady;
            }

            if (change.NewValue is IVideoFrameSource source && IsAttachedToVisualTree())
            {
                source.FrameReady += OnFrameReady;
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Source is { } source)
        {
            source.FrameReady += OnFrameReady;
            UpdateFrame();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (Source is { } source)
        {
            source.FrameReady -= OnFrameReady;
        }
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    private void OnFrameReady(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _updateScheduled, 1) == 0)
        {
            Dispatcher.UIThread.Post(UpdateFrame, DispatcherPriority.Render);
        }
    }

    private void UpdateFrame()
    {
        Interlocked.Exchange(ref _updateScheduled, 0);
        var hasFrame = Source?.ReadLatestFrame(CopyToBitmap) ?? false;
        if (!hasFrame)
        {
            // Vídeo fechado: não deixa o último quadro da aula anterior na tela.
            _bitmap = null;
        }

        InvalidateVisual();
    }

    private unsafe void CopyToBitmap(ReadOnlySpan<byte> pixels, int width, int height, int stride)
    {
        if (_bitmap is null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
        {
            _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        }

        using var buffer = _bitmap.Lock();
        fixed (byte* source = pixels)
        {
            var rowBytes = width * 4;
            for (var row = 0; row < height; row++)
            {
                Buffer.MemoryCopy(source + (row * stride), (byte*)buffer.Address + (row * buffer.RowBytes), buffer.RowBytes, rowBytes);
            }
        }
    }
}
