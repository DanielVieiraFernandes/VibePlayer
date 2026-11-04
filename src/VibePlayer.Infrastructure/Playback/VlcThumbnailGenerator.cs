using System.Globalization;
using LibVLCSharp.Shared;
using SkiaSharp;
using VibePlayer.Core.Playback;

namespace VibePlayer.Infrastructure.Playback;

/// <summary>
/// Tira um quadro de um vídeo sem janela e grava em JPEG.
/// </summary>
/// <remarks>
/// Usa o mesmo caminho da reprodução (callbacks de vídeo em memória) com um player próprio, sem
/// áudio, começando na posição pedida; guarda o primeiro quadro e para. O "snapshot" do VLC não
/// serve porque exige uma saída de vídeo visível. A imagem final tem 320 × 180, com faixas pretas
/// para manter a proporção (vídeos verticais ou 4:3 não ficam esticados).
/// </remarks>
public sealed class VlcThumbnailGenerator(VlcRuntime runtime) : IThumbnailGenerator
{
    public const int Width = 320;
    public const int Height = 180;
    private const int JpegQuality = 80;
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(8);

    public async Task<bool> GenerateAsync(string videoPath, TimeSpan at, string outputPath, CancellationToken cancellationToken = default)
    {
        if (runtime.LibVlc is not { } libVlc || !File.Exists(videoPath))
        {
            return false;
        }

        // Ordem de liberação importa: o player primeiro (para de chamar as callbacks), o buffer
        // de quadros depois. Com "using var" a ordem seria a inversa.
        var player = new MediaPlayer(libVlc) { EnableKeyInput = false, EnableMouseInput = false };
        var frames = new VlcFrameBuffer(player);
        using var media = new Media(libVlc, new Uri(videoPath));
        media.AddOption(":start-time=" + at.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
        media.AddOption(":no-audio");

        var firstFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        frames.FrameReady += (_, _) => firstFrame.TrySetResult();
        player.EncounteredError += (_, _) => firstFrame.TrySetCanceled();
        player.EndReached += (_, _) => firstFrame.TrySetCanceled();

        player.Media = media;
        player.Play();
        try
        {
            await firstFrame.Task.WaitAsync(FrameTimeout, cancellationToken).ConfigureAwait(false);
            SKBitmap? image = null;
            frames.ReadLatestFrame((pixels, width, height, stride) => image = ToBitmap(pixels, width, height, stride));
            return image is not null && Save(image, outputPath);
        }
        catch (Exception ex) when (ex is TimeoutException or TaskCanceledException)
        {
            return false;
        }
        finally
        {
            // Fora das callbacks do VLC (ver regra de threads em VlcMediaPlayer).
            player.Stop();
            player.Dispose();
            frames.Dispose();
        }
    }

    private static unsafe SKBitmap ToBitmap(ReadOnlySpan<byte> pixels, int width, int height, int stride)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        fixed (byte* source = pixels)
        {
            for (var row = 0; row < height; row++)
            {
                Buffer.MemoryCopy(source + (row * stride), (byte*)bitmap.GetPixels() + (row * bitmap.RowBytes), bitmap.RowBytes, width * 4);
            }
        }

        return bitmap;
    }

    private static bool Save(SKBitmap frame, string outputPath)
    {
        using (frame)
        using (var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Opaque)))
        {
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Black);
            var scale = Math.Min(Width / (float)frame.Width, Height / (float)frame.Height);
            var target = SKRect.Create((Width - (frame.Width * scale)) / 2, (Height - (frame.Height * scale)) / 2, frame.Width * scale, frame.Height * scale);
            using var image = SKImage.FromBitmap(frame);
            canvas.DrawImage(image, target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

            using var snapshot = surface.Snapshot();
            using var data = snapshot.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            // Grava num temporário e renomeia: uma miniatura cortada pela metade nunca parece pronta.
            var temp = outputPath + ".tmp";
            using (var file = File.Create(temp))
            {
                data.SaveTo(file);
            }

            File.Move(temp, outputPath, overwrite: true);
            return true;
        }
    }
}
