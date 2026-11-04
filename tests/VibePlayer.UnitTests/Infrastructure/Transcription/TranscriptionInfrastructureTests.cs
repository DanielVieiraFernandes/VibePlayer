using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VibePlayer.Core;
using VibePlayer.Core.Transcription;
using VibePlayer.Infrastructure.Transcription;
using VibePlayer.Infrastructure.Transcription.Worker;
using VibePlayer.UnitTests.Core.Library;

namespace VibePlayer.UnitTests.Infrastructure.Transcription;

public sealed class PtsGapFillerTests
{
    private static short[] Block(int samples) => Enumerable.Repeat((short)16384, samples).ToArray();

    [Fact]
    public void Append_BlocosEmSequencia_NaoInsereSilencio()
    {
        var filler = new PtsGapFiller();

        filler.Append(Block(1600), 5_000_000);
        filler.Append(Block(1600), 5_100_000);

        filler.Count.ShouldBe(3200);
        filler.SilenceSamples.ShouldBe(0);
    }

    [Fact]
    public void Append_PosicaoPassaDoLimite_InsereSilencio()
    {
        var filler = new PtsGapFiller();

        filler.Append(Block(1600), 5_000_000);
        filler.Append(Block(1600), 35_100_000);

        filler.Count.ShouldBe(483_200);
        filler.Samples.Span[1000].ShouldBe(0.5f);
        filler.Samples.Span[2000].ShouldBe(0f);
        filler.Samples.Span[481_700].ShouldBe(0.5f);
    }

    [Fact]
    public void Append_DiferencaPequena_NaoViraSilencio()
    {
        var filler = new PtsGapFiller();

        filler.Append(Block(1600), 0);
        filler.Append(Block(1600), 150_000);

        filler.SilenceSamples.ShouldBe(0);
    }

    [Fact]
    public void Append_AlemDaCapacidade_Cresce()
    {
        var filler = new PtsGapFiller(10);

        filler.Append(Block(50_000), 0);

        filler.Count.ShouldBe(50_000);
    }
}

public sealed class SubtitleSourceTests
{
    private readonly FakeFileSystem _files = new();
    private readonly PhysicalSubtitleSource _source;

    public SubtitleSourceTests()
    {
        _source = new PhysicalSubtitleSource(_files);
    }

    [Theory]
    [InlineData("aula.srt")]
    [InlineData("aula.vtt")]
    [InlineData("aula.pt.srt")]
    [InlineData("aula.pt-BR.vtt")]
    [InlineData("AULA.SRT")]
    public void Find_VariacoesDeNome_Encontra(string name)
    {
        var video = _files.AddFile("curso/aula.mp4");
        var subtitle = _files.AddFile($"curso/{name}");

        _source.Find(video).ShouldBe(subtitle);
    }

    [Fact]
    public void Find_VariacoesDeNome_EscolheNaOrdem()
    {
        var video = _files.AddFile("curso/aula.mp4");
        _files.AddFile("curso/aula.en.srt");
        _files.AddFile("curso/aula.vtt");
        var plain = _files.AddFile("curso/aula.srt");
        _source.Find(video).ShouldBe(plain);

        _files.AddFile("curso/aula.pt.vtt");
        var portuguese = _files.AddFile("curso/aula.pt-BR.srt");
        _source.Find(video).ShouldBe(portuguese);
    }

    [Theory]
    [InlineData("aula 2.srt")]
    [InlineData("aula.txt")]
    [InlineData("aula.parte.dois.srt")]
    [InlineData("outra.srt")]
    public void Find_OutrosArquivos_Ignora(string name)
    {
        var video = _files.AddFile("curso/aula.mp4");
        _files.AddFile($"curso/{name}");

        _source.Find(video).ShouldBeNull();
    }
}

public sealed class WhisperModelStoreTests : IDisposable
{
    private static readonly byte[] Content = Enumerable.Range(0, 5000).Select(i => (byte)i).ToArray();

    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly FakeHandler _handler = new(Content);
    private readonly HttpClient _client;
    private readonly WhisperModel _model = new("teste", "teste.bin", Content.Length, Convert.ToHexStringLower(SHA256.HashData(Content)));
    private long _freeSpace = long.MaxValue;
    private readonly WhisperModelStore _store;

    public WhisperModelStoreTests()
    {
        Directory.CreateDirectory(_directory);
        _client = new HttpClient(_handler);
        _store = new WhisperModelStore(new Paths(_directory), _client, _ => _freeSpace, new ListLogger<WhisperModelStore>());
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Partial => _store.PathOf(_model) + ".part";

    public void Dispose()
    {
        _client.Dispose();
        _handler.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task Download_Completo_FicaDisponivel()
    {
        _store.IsAvailable(_model).ShouldBeFalse();

        await _store.DownloadAsync(_model, null, Token);

        _store.IsAvailable(_model).ShouldBeTrue();
        File.Exists(Partial).ShouldBeFalse();
        (await File.ReadAllBytesAsync(_store.PathOf(_model), Token)).ShouldBe(Content);
    }

    [Fact]
    public async Task Download_ArquivoParcial_RetomaComRange()
    {
        await File.WriteAllBytesAsync(Partial, Content[..1000], Token);

        await _store.DownloadAsync(_model, null, Token);

        _handler.LastRange.ShouldBe(1000);
        (await File.ReadAllBytesAsync(_store.PathOf(_model), Token)).ShouldBe(Content);
    }

    [Fact]
    public async Task Download_ServidorIgnoraRange_RecomecaDoZero()
    {
        _handler.SupportsRange = false;
        await File.WriteAllBytesAsync(Partial, new byte[1000], Token);

        await _store.DownloadAsync(_model, null, Token);

        (await File.ReadAllBytesAsync(_store.PathOf(_model), Token)).ShouldBe(Content);
    }

    [Fact]
    public async Task Download_HashDiferente_DescartaEFalha()
    {
        _handler.Content = new byte[Content.Length];

        var error = await Should.ThrowAsync<ModelDownloadException>(() => _store.DownloadAsync(_model, null, Token));

        error.Error.ShouldBe(ModelDownloadError.HashMismatch);
        File.Exists(Partial).ShouldBeFalse();
        _store.IsAvailable(_model).ShouldBeFalse();
    }

    [Fact]
    public async Task Download_SemEspaco_FalhaAntesDeBaixar()
    {
        _freeSpace = 10;

        var error = await Should.ThrowAsync<ModelDownloadException>(() => _store.DownloadAsync(_model, null, Token));

        error.Error.ShouldBe(ModelDownloadError.NotEnoughSpace);
        _handler.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task Download_ServidorFora_InformaRede()
    {
        _handler.Status = HttpStatusCode.ServiceUnavailable;

        var error = await Should.ThrowAsync<ModelDownloadException>(() => _store.DownloadAsync(_model, null, Token));

        error.Error.ShouldBe(ModelDownloadError.Network);
    }

    [Fact]
    public async Task Download_Progresso_ChegaAoFim()
    {
        var values = new List<double>();

        await _store.DownloadAsync(_model, new InlineProgress(values.Add), Token);

        values[^1].ShouldBe(1);
    }

    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    private sealed class Paths(string directory) : IAppPaths
    {
        public string DataDirectory => directory;

        public string LogsDirectory => directory;

        public string DatabasePath => Path.Combine(directory, "db");

        public string BackupsDirectory => directory;

        public string ThumbnailsDirectory => directory;

        public string ModelsDirectory => directory;
    }

    private sealed class FakeHandler(byte[] content) : HttpMessageHandler
    {
        public byte[] Content { get; set; } = content;

        public bool SupportsRange { get; set; } = true;

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public long? LastRange { get; private set; }

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (Status != HttpStatusCode.OK)
            {
                return Task.FromResult(new HttpResponseMessage(Status));
            }

            LastRange = request.Headers.Range?.Ranges.Single().From;
            var ranged = SupportsRange && LastRange is not null;
            var body = ranged ? Content[(int)LastRange!.Value..] : Content;
            var response = new HttpResponseMessage(ranged ? HttpStatusCode.PartialContent : HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
            if (ranged)
            {
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(LastRange!.Value, Content.Length - 1, Content.Length);
            }

            return Task.FromResult(response);
        }
    }
}

public sealed class WorkerEntryPointTests
{
    private static readonly float[] Samples = [0f, 0.1f];
    private readonly IAudioExtractor _extractor = Substitute.For<IAudioExtractor>();
    private readonly ISpeechTranscriber _transcriber = Substitute.For<ISpeechTranscriber>();
    private readonly TranscriptionJob _job = new("/aula.mp4", "/m.bin", "auto", 2, false);

    [Fact]
    public async Task Run_AudioETexto_EmiteEtapasESegmentos()
    {
        Extraction(new AudioExtractionResult(AudioExtractionStatus.Completed, Samples));
        _transcriber.TranscribeAsync(Arg.Any<ReadOnlyMemory<float>>(), Arg.Any<TranscriptionJob>(), Arg.Any<Action<string>>(), Arg.Any<Action<double>>(), Arg.Any<Action<TranscriptSegment>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.ArgAt<Action<string>>(2)("Vulkan");
            call.ArgAt<Action<TranscriptSegment>>(4)(new TranscriptSegment(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "olá"));
            return Task.FromResult<string?>("pt");
        });

        var events = await RunAsync();

        events.Select(e => e.Kind).ShouldBe([WorkerEventKind.Stage, WorkerEventKind.Stage, WorkerEventKind.Runtime, WorkerEventKind.Segment, WorkerEventKind.Done]);
        events[^1].Value.ShouldBe("pt");
        events[3].Text.ShouldBe("olá");
    }

    [Fact]
    public async Task Run_SemAudio_InformaENaoTranscreve()
    {
        Extraction(new AudioExtractionResult(AudioExtractionStatus.NoAudio, default));

        (await RunAsync())[^1].Kind.ShouldBe(WorkerEventKind.NoAudio);

        await _transcriber.DidNotReceive().TranscribeAsync(Arg.Any<ReadOnlyMemory<float>>(), Arg.Any<TranscriptionJob>(), Arg.Any<Action<string>>(), Arg.Any<Action<double>>(), Arg.Any<Action<TranscriptSegment>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_ArquivoIlegivel_InformaOCodigo()
    {
        Extraction(new AudioExtractionResult(AudioExtractionStatus.Unreadable, default));

        (await RunAsync())[^1].ShouldBe(new WorkerEvent(WorkerEventKind.Error, Value: TranscriptionErrors.UnreadableFile));
    }

    [Fact]
    public async Task Run_SemVlc_InformaOCodigo()
    {
        _extractor.ExtractAsync(Arg.Any<string>(), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>()).ThrowsAsync(new DllNotFoundException());

        (await RunAsync())[^1].Value.ShouldBe(TranscriptionErrors.VlcUnavailable);
    }

    [Fact]
    public async Task Run_EntradaVazia_SaiComErroSemEventos()
    {
        var output = new StringWriter();
        var worker = new WorkerEntryPoint(_extractor, _transcriber);

        var code = await worker.RunAsync(new StringReader(string.Empty), output, () => { }, TestContext.Current.CancellationToken);

        code.ShouldBe(2);
        output.ToString().ShouldBeEmpty();
    }

    private void Extraction(AudioExtractionResult result) =>
        _extractor.ExtractAsync(Arg.Any<string>(), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>()).Returns(result);

    private async Task<WorkerEvent[]> RunAsync()
    {
        var output = new StringWriter();
        var worker = new WorkerEntryPoint(_extractor, _transcriber);
        await worker.RunAsync(new StringReader(WorkerProtocol.Serialize(_job)), output, () => { }, TestContext.Current.CancellationToken);
        return output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select(line => WorkerProtocol.TryParseEvent(line)!).ToArray();
    }
}
