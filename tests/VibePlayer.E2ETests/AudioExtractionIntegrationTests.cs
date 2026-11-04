using VibePlayer.Infrastructure.Transcription.Worker;

namespace VibePlayer.E2ETests;

/// <summary>
/// A extração real, com o VLC, nos três SOs. Os arquivos de tests/fixtures/audio têm um tom em
/// tempo conhecido, porque só a duração não mostraria um áudio deslocado: gerados com aevalsrc do
/// ffmpeg, sem conteúdo de terceiros. No Linux exige o VLC do sistema (a CI instala).
/// </summary>
public sealed class AudioExtractionIntegrationTests
{
    private const int SampleRate = PtsGapFiller.SampleRate;
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(100);

    private readonly VlcAudioExtractor _extractor = new();

    public static TheoryData<string> Samples => ["videos/amostra-h264.mp4", "videos/amostra-h265.mkv", "videos/amostra-mpeg4.avi", "videos/amostra-vp9.webm"];

    [Theory]
    [MemberData(nameof(Samples))]
    public async Task Extract_FormatosDosFixtures_SaiPcm16kMonoComDuracaoDoVideo(string sample)
    {
        var result = await Extract(sample);

        result.Status.ShouldBe(AudioExtractionStatus.Completed);
        Duration(result).TotalSeconds.ShouldBe(3, Tolerance.TotalSeconds);
        result.Samples.Span.ToArray().Max(Math.Abs).ShouldBeLessThanOrEqualTo(1f);
    }

    [Fact]
    public async Task Extract_TomDe60s_NaoDeriva()
    {
        var result = await Extract("audio/marcador-60s.m4a");

        result.Status.ShouldBe(AudioExtractionStatus.Completed);
        Duration(result).TotalSeconds.ShouldBe(60, Tolerance.TotalSeconds);
        FirstLoudSample(result).TotalSeconds.ShouldBe(50, Tolerance.TotalSeconds);
    }

    [Fact]
    public async Task Extract_ComBuracoDePts_PreencheComSilencio()
    {
        var result = await Extract("audio/buraco-30s.mkv");

        result.Status.ShouldBe(AudioExtractionStatus.Completed);
        Duration(result).TotalSeconds.ShouldBe(80, Tolerance.TotalSeconds);
        FirstLoudSample(result).TotalSeconds.ShouldBe(70, Tolerance.TotalSeconds);
    }

    [Fact]
    public async Task Extract_SemFaixaDeAudio_InformaSemAudio()
    {
        var result = await Extract("audio/sem-audio.mp4");

        result.Status.ShouldBe(AudioExtractionStatus.NoAudio);
    }

    [Fact]
    public async Task Extract_ArquivoCorrompido_InformaErro()
    {
        var result = await Extract("videos/corrompido.mp4");

        result.Status.ShouldBe(AudioExtractionStatus.Unreadable);
    }

    [Fact]
    public async Task Extract_Progresso_ChegaPertoDoFim()
    {
        var reported = new List<double>();
        var progress = new SynchronousProgress(value =>
        {
            lock (reported)
            {
                reported.Add(value);
            }
        });

        await _extractor.ExtractAsync(Fixture("audio/marcador-60s.m4a"), progress, TestContext.Current.CancellationToken);

        reported.ShouldNotBeEmpty();
        reported.Max().ShouldBeGreaterThan(0.9);
    }

    private Task<AudioExtractionResult> Extract(string sample) =>
        _extractor.ExtractAsync(Fixture(sample), null, TestContext.Current.CancellationToken);

    private static TimeSpan Duration(AudioExtractionResult result) =>
        TimeSpan.FromSeconds(result.Samples.Length / (double)SampleRate);

    private static TimeSpan FirstLoudSample(AudioExtractionResult result)
    {
        var index = result.Samples.Span.ToArray().AsSpan().IndexOfAnyExceptInRange(-0.25f, 0.25f);
        index.ShouldBeGreaterThanOrEqualTo(0, "o tom não apareceu no áudio");
        return TimeSpan.FromSeconds(index / (double)SampleRate);
    }

    private static string Fixture(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests", "fixtures")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull();
        return Path.GetFullPath(Path.Combine(directory.FullName, "tests", "fixtures", relativePath));
    }

    private sealed class SynchronousProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
