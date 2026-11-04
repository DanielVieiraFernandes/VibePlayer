using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using VibePlayer.Core.Playback;
using VibePlayer.Infrastructure.Playback;

namespace VibePlayer.E2ETests;

/// <summary>
/// O VLC de verdade, sem janela: abre, toca, informa erro, lê duração e gera miniatura com as
/// amostras de tests/fixtures/videos (vídeos de 3 s gerados com padrões de teste, sem conteúdo de
/// terceiros). No Linux exige o VLC do sistema (a CI instala).
/// </summary>
// Coleção com os testes de extração e de processo. Duas instâncias do LibVLC subindo juntas no
// processo de testes atrapalham a reprodução, e um filho aberto no Windows herda os arquivos nativos
// abertos no momento (a miniatura gravada aqui, por exemplo), que ficam presos enquanto ele viver.
[Collection("VLC real")]
public sealed class VlcIntegrationTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly VlcRuntime _runtime = new(NullLogger<VlcRuntime>.Instance);
    private readonly VlcMediaPlayer _player;

    public VlcIntegrationTests()
    {
        _player = new VlcMediaPlayer(_runtime, NullLogger<VlcMediaPlayer>.Instance);
    }

    public void Dispose()
    {
        _player.Dispose();
        _runtime.Dispose();
    }

    public static TheoryData<string> Samples => ["amostra-h264.mp4", "amostra-h265.mkv", "amostra-vp9.webm", "amostra-mpeg4.avi"];

    [Theory]
    [MemberData(nameof(Samples))]
    public async Task TocaAmostras(string sample)
    {
        RequireVlc();
        var frame = WaitForFrame();

        _player.Open(Fixture(sample), TimeSpan.Zero, autoPlay: true);

        await WaitUntil(() => _player.State == PlaybackState.Playing, "tocando");
        await frame.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await WaitUntil(() => _player.Duration > TimeSpan.FromSeconds(2.5), "duração");
        _player.Video.ReadLatestFrame((pixels, width, height, _) =>
        {
            width.ShouldBe(320);
            height.ShouldBe(180);
            pixels.Length.ShouldBeGreaterThan(0);
        }).ShouldBeTrue();
    }

    [Fact]
    public void Dispose_LogoDepoisDeComecarATocar_NaoDerrubaOProcesso()
    {
        RequireVlc();
        var player = new VlcMediaPlayer(_runtime, NullLogger<VlcMediaPlayer>.Instance);
        using var release = new ManualResetEventSlim();

        // Ocupa o pool antes de o player agendar o volume, no próprio aviso de "tocando": a tarefa
        // dele fica atrás destas, e só roda depois do Dispose. Sem a proteção, o processo inteiro cai.
        ThreadPool.GetMinThreads(out var workers, out _);
        var blocked = 0;
        player.StateChanged += (_, _) =>
        {
            if (player.State == PlaybackState.Playing && Interlocked.Exchange(ref blocked, 1) == 0)
            {
                for (var i = 0; i < workers * 2; i++)
                {
                    ThreadPool.QueueUserWorkItem(_ => release.Wait());
                }
            }
        };

        player.Open(Fixture("amostra-h264.mp4"), TimeSpan.Zero, autoPlay: true);
        SpinWait.SpinUntil(() => Volatile.Read(ref blocked) == 1, Timeout).ShouldBeTrue("não começou a tocar");

        player.Dispose();
        release.Set();

        // Dá tempo de a tarefa adiada rodar sobre o player liberado.
        Thread.Sleep(500);
    }

    [Fact]
    public async Task ChegaAoFim_EToca_DeNovo()
    {
        RequireVlc();
        _player.Open(Fixture("amostra-h264.mp4"), TimeSpan.FromSeconds(2), autoPlay: true);

        await WaitUntil(() => _player.State == PlaybackState.Ended, "fim");

        // Abrir de novo logo após o fim (como "próxima aula") não pode travar o VLC.
        _player.Open(Fixture("amostra-vp9.webm"), TimeSpan.Zero, autoPlay: true);
        await WaitUntil(() => _player.State == PlaybackState.Playing, "tocando a seguinte");
    }

    [Fact]
    public async Task AbrirPausado_FicaNaPosicaoSemTocar()
    {
        RequireVlc();

        _player.Open(Fixture("amostra-h264.mp4"), TimeSpan.FromSeconds(1.5), autoPlay: false);

        // Pausado antes do primeiro quadro: o VLC não desenha nada até tocar (o app mostra o
        // pôster nesse estado). Tocar a partir daí começa na posição pedida.
        await WaitUntil(() => _player.State == PlaybackState.Paused, "pausado");
        _player.Position.TotalSeconds.ShouldBe(1.5, tolerance: 0.6);

        var frame = WaitForFrame();
        _player.Play();
        await frame.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        _player.Position.TotalSeconds.ShouldBeGreaterThanOrEqualTo(1.4);
    }

    [Fact]
    public async Task ArquivoCorrompido_InformaErro()
    {
        RequireVlc();

        _player.Open(Fixture("corrompido.mp4"), TimeSpan.Zero, autoPlay: true);

        await WaitUntil(() => _player.State is PlaybackState.Error or PlaybackState.Ended, "erro");
    }

    [Fact]
    public void ArquivoInexistente_InformaErroSemChamarOVlc()
    {
        _player.Open(Fixture("nao-existe.mp4"), TimeSpan.Zero, autoPlay: true);

        _player.State.ShouldBe(PlaybackState.Error);
        _player.Error.ShouldBe(PlaybackError.FileNotFound);
    }

    [Fact]
    public async Task LeDuracao()
    {
        RequireVlc();

        var duration = await new VlcMediaInfoReader(_runtime).ReadDurationAsync(Fixture("amostra-vp9.webm"), TestContext.Current.CancellationToken);

        duration.ShouldNotBeNull().TotalSeconds.ShouldBe(3, tolerance: 0.2);
    }

    [Fact]
    public async Task GeraMiniatura()
    {
        RequireVlc();
        var output = Path.Combine(Path.GetTempPath(), "VibePlayerThumb", Guid.NewGuid().ToString("N") + ".jpg");

        var ok = await new VlcThumbnailGenerator(_runtime).GenerateAsync(Fixture("amostra-h264.mp4"), TimeSpan.FromSeconds(0.3), output, TestContext.Current.CancellationToken);

        ok.ShouldBeTrue();
        using var image = SKBitmap.Decode(output);
        image.Width.ShouldBe(VlcThumbnailGenerator.Width);
        image.Height.ShouldBe(VlcThumbnailGenerator.Height);
        File.Delete(output);
    }

    private void RequireVlc() =>
        _runtime.Availability.ShouldBe(PlayerAvailability.Available, "VLC não carregou (no Linux: sudo apt install vlc)");

    private Task WaitForFrame()
    {
        var frame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _player.Video.FrameReady += (_, _) => frame.TrySetResult();
        return frame.Task;
    }

    private static string Fixture(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests", "fixtures", "videos")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "tests", "fixtures", "videos", name);
    }

    private static async Task WaitUntil(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline, $"Tempo esgotado esperando: {what}");
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }
}
