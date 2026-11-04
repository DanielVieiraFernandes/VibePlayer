using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using VibePlayer.Core.Library;
using VibePlayer.Core.Settings;
using VibePlayer.Core.Transcription;
using VibePlayer.Infrastructure.Persistence;
using VibePlayer.Infrastructure.Settings;
using VibePlayer.Infrastructure.Transcription;
using VibePlayer.UnitTests.Core.Library;

namespace VibePlayer.UnitTests.Infrastructure.Transcription;

/// <summary>
/// A fila com o banco real em memória e um worker programado pelo teste: as regras de cache, fila
/// e falha são justamente as que dependem do que foi gravado.
/// </summary>
public sealed class TranscriptionServiceTests : IDisposable
{
    private const string Hash = "hash-do-video";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentMemoryDatabase _database = new();
    private readonly ManualTimeProvider _time = new();
    private readonly FakeFileSystem _files = new();
    private readonly ScriptedWorker _worker = new();
    private readonly IWhisperModelStore _models = Substitute.For<IWhisperModelStore>();
    private readonly IFileHasher _hasher = Substitute.For<IFileHasher>();
    private readonly ListLogger<TranscriptionService> _logger = new();
    private readonly TranscriptStore _store;
    private readonly SqliteSettingsStore _settings;
    private readonly PhysicalSubtitleSourceFake _subtitles = new();
    private readonly TranscriptionService _service;

    public TranscriptionServiceTests()
    {
        _store = new TranscriptStore(_database, _time);
        _settings = new SqliteSettingsStore(_database, _time, new ListLogger<SqliteSettingsStore>());
        _models.IsAvailable(Arg.Any<WhisperModel>()).Returns(true);
        _models.PathOf(Arg.Any<WhisperModel>()).Returns("/modelos/m.bin");
        _hasher.ComputeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => Task.FromResult($"{Hash}-{Path.GetFileName(call.Arg<string>())}"));
        _service = NewService();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _service.Dispose();
        _database.Dispose();
    }

    [Fact]
    public async Task Get_SemNada_FicaNaoTranscrita()
    {
        var lesson = AddLesson("a.mp4");

        (await _service.GetAsync(lesson, Token)).Status.ShouldBe(TranscriptionStatus.NotTranscribed);
    }

    [Fact]
    public async Task Run_Sucesso_GravaSegmentosFiltradosEVinculaAAula()
    {
        var lesson = AddLesson("a.mp4");
        _worker.Script(Segment("Olá"), Segment("[MÚSICA]"), new WorkerEvent(WorkerEventKind.Done, Value: "pt"));

        await EnqueueAndWait(lesson, TranscriptionStatus.Done);

        var result = await _service.GetAsync(lesson, Token);
        result.Transcript!.Segments.ShouldHaveSingleItem().Text.ShouldBe("Olá");
        result.Transcript.Language.ShouldBe("pt");
        result.Transcript.Model.ShouldBe(WhisperModels.SmallQuantized.Name);
        result.Transcript.Source.ShouldBe(TranscriptSource.Whisper);
    }

    [Fact]
    public async Task Run_EventosDeProgresso_ChegamAoAssinante()
    {
        var lesson = AddLesson("a.mp4");
        var progress = new List<TranscriptionProgress>();
        _service.ProgressChanged += (_, value) =>
        {
            lock (progress)
            {
                progress.Add(value);
            }
        };
        _worker.Script(
            new WorkerEvent(WorkerEventKind.Stage, TranscriptionStage.Extracting),
            new WorkerEvent(WorkerEventKind.Progress, Fraction: 0.5),
            new WorkerEvent(WorkerEventKind.Stage, TranscriptionStage.Transcribing),
            new WorkerEvent(WorkerEventKind.Progress, Fraction: 0.25),
            new WorkerEvent(WorkerEventKind.Done));

        await EnqueueAndWait(lesson, TranscriptionStatus.Done);

        progress.ShouldContain(new TranscriptionProgress(lesson, TranscriptionStage.Extracting, 0.5));
        progress.ShouldContain(new TranscriptionProgress(lesson, TranscriptionStage.Transcribing, 0.25));
    }

    [Fact]
    public async Task Run_AntesDoFim_NadaFicaGravado()
    {
        var lesson = AddLesson("a.mp4");
        var gate = _worker.ScriptGated(Segment("Olá"));
        await _service.EnqueueAsync([lesson], Token);
        await gate.Reached.WaitAsync(Timeout, Token);

        var db = _database.CreateDbContext();
        db.TranscriptSegments.Count().ShouldBe(0);
        db.Dispose();
        await WaitForStatus(lesson, TranscriptionStatus.Running);

        gate.Release(new WorkerEvent(WorkerEventKind.Done));
        await WaitForStatus(lesson, TranscriptionStatus.Done);
    }

    [Fact]
    public async Task Run_WorkerInformaSemAudio_FicaComoSemAudioENaoRepete()
    {
        var lesson = AddLesson("a.mp4");
        _worker.Script(new WorkerEvent(WorkerEventKind.NoAudio));
        await EnqueueAndWait(lesson, TranscriptionStatus.NoAudio);

        await _service.EnqueueAsync([lesson], Token);

        (await _service.GetAsync(lesson, Token)).Status.ShouldBe(TranscriptionStatus.NoAudio);
        _worker.Jobs.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Run_ArquivoIlegivel_FalhaEPermiteTentarDeNovo()
    {
        var lesson = AddLesson("a.mp4");
        _worker.Script(new WorkerEvent(WorkerEventKind.Error, Value: TranscriptionErrors.UnreadableFile));
        await EnqueueAndWait(lesson, TranscriptionStatus.Failed);
        (await _service.GetAsync(lesson, Token)).ErrorCode.ShouldBe(TranscriptionErrors.UnreadableFile);

        _worker.Script(new WorkerEvent(WorkerEventKind.Done));
        await EnqueueAndWait(lesson, TranscriptionStatus.Done);
    }

    [Fact]
    public async Task Run_WorkerMorre_MarcaFalhaESegueAFila()
    {
        var first = AddLesson("a.mp4");
        var second = AddLesson("b.mp4");
        _worker.ScriptCrash(exitCode: 139);
        _worker.Script(new WorkerEvent(WorkerEventKind.Done));

        await _service.EnqueueAsync([first, second], Token);
        await WaitForStatus(second, TranscriptionStatus.Done);

        var failed = await _service.GetAsync(first, Token);
        failed.Status.ShouldBe(TranscriptionStatus.Failed);
        failed.ErrorCode.ShouldBe(TranscriptionErrors.ProcessCrashed);
        _logger.Entries.ShouldContain(entry => entry.Message.Contains("139", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Run_FalhaComGpu_RepeteNaCpuEGravaPreferencia()
    {
        var lesson = AddLesson("a.mp4");
        _worker.ScriptCrash(exitCode: 1, new WorkerEvent(WorkerEventKind.Runtime, Value: "Vulkan"));
        _worker.Script(new WorkerEvent(WorkerEventKind.Done));

        await EnqueueAndWait(lesson, TranscriptionStatus.Done);

        _worker.Jobs.Select(job => job.ForceCpu).ShouldBe([false, true]);
        (await _settings.GetAsync(TranscriptionSettings.ForceCpu, Token)).ShouldBeTrue();
    }

    [Fact]
    public async Task Run_FalhaNaCpu_NaoRepete()
    {
        var lesson = AddLesson("a.mp4");
        _worker.ScriptCrash(exitCode: 1, new WorkerEvent(WorkerEventKind.Runtime, Value: "Cpu"));

        await EnqueueAndWait(lesson, TranscriptionStatus.Failed);

        _worker.Jobs.Count.ShouldBe(1);
        (await _settings.GetAsync(TranscriptionSettings.ForceCpu, Token)).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_SemModelo_Falha()
    {
        var lesson = AddLesson("a.mp4");
        _models.IsAvailable(Arg.Any<WhisperModel>()).Returns(false);

        await EnqueueAndWait(lesson, TranscriptionStatus.Failed);

        (await _service.GetAsync(lesson, Token)).ErrorCode.ShouldBe(TranscriptionErrors.ModelMissing);
        _worker.Jobs.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ArquivoSumiu_Falha()
    {
        var lesson = AddLesson("a.mp4", createFile: false);

        await EnqueueAndWait(lesson, TranscriptionStatus.Failed);

        (await _service.GetAsync(lesson, Token)).ErrorCode.ShouldBe(TranscriptionErrors.FileMissing);
    }

    [Fact]
    public async Task Enqueue_AulaAberta_VaiParaAFrente()
    {
        var first = AddLesson("a.mp4");
        var second = AddLesson("b.mp4");
        var third = AddLesson("c.mp4");
        var gate = _worker.ScriptGated();
        _worker.Script(new WorkerEvent(WorkerEventKind.Done));
        _worker.Script(new WorkerEvent(WorkerEventKind.Done));
        await _service.EnqueueAsync([first, second, third], Token);
        await gate.Reached.WaitAsync(Timeout, Token);

        _service.SetCurrentLesson(third);
        gate.Release(new WorkerEvent(WorkerEventKind.Done));
        await WaitForStatus(second, TranscriptionStatus.Done);

        _worker.Jobs.Select(job => Path.GetFileName(job.VideoPath)).ShouldBe(["a.mp4", "c.mp4", "b.mp4"]);
    }

    [Fact]
    public async Task Run_VariasAulas_UmaDeCadaVez()
    {
        var first = AddLesson("a.mp4");
        var second = AddLesson("b.mp4");
        var gate = _worker.ScriptGated();
        _worker.Script(new WorkerEvent(WorkerEventKind.Done));
        await _service.EnqueueAsync([first, second], Token);
        await gate.Reached.WaitAsync(Timeout, Token);

        (await _service.GetAsync(second, Token)).Status.ShouldBe(TranscriptionStatus.Queued);
        _worker.Jobs.Count.ShouldBe(1);

        gate.Release(new WorkerEvent(WorkerEventKind.Done));
        await WaitForStatus(second, TranscriptionStatus.Done);
    }

    [Fact]
    public async Task Cancel_EmAndamento_VoltaANaoTranscrita()
    {
        var lesson = AddLesson("a.mp4");
        var gate = _worker.ScriptGated();
        await _service.EnqueueAsync([lesson], Token);
        await gate.Reached.WaitAsync(Timeout, Token);

        await _service.CancelAsync(lesson, Token);

        await WaitUntil(async () => await _store.GetRecordAsync(lesson, Token) is null);
        (await _service.GetAsync(lesson, Token)).Status.ShouldBe(TranscriptionStatus.NotTranscribed);
    }

    [Fact]
    public async Task Cancel_NaFila_TiraDaFila()
    {
        var first = AddLesson("a.mp4");
        var second = AddLesson("b.mp4");
        var gate = _worker.ScriptGated();
        await _service.EnqueueAsync([first, second], Token);
        await gate.Reached.WaitAsync(Timeout, Token);

        await _service.CancelAsync(second, Token);
        gate.Release(new WorkerEvent(WorkerEventKind.Done));

        await WaitForStatus(first, TranscriptionStatus.Done);
        (await _service.GetAsync(second, Token)).Status.ShouldBe(TranscriptionStatus.NotTranscribed);
        _worker.Jobs.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Resume_AulaEmAndamentoAoFechar_Recomeca()
    {
        var running = AddLesson("a.mp4");
        var queued = AddLesson("b.mp4");
        await _store.SaveRecordAsync(new LessonTranscriptionRecord(running, TranscriptionStatus.Running, QueuedAt: _time.GetUtcNow()), Token);
        await _store.SaveRecordAsync(new LessonTranscriptionRecord(queued, TranscriptionStatus.Queued, QueuedAt: _time.GetUtcNow().AddSeconds(1)), Token);
        _worker.Script(new WorkerEvent(WorkerEventKind.Done));
        _worker.Script(new WorkerEvent(WorkerEventKind.Done));

        await _service.ResumeAsync(Token);

        await WaitForStatus(queued, TranscriptionStatus.Done);
        _worker.Jobs.Select(job => Path.GetFileName(job.VideoPath)).ShouldBe(["a.mp4", "b.mp4"]);
    }

    [Fact]
    public async Task Enqueue_MesmoHash_ReaproveitaSemWorker()
    {
        var first = AddLesson("a.mp4");
        var copy = AddLesson("copia.mp4");
        _hasher.ComputeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Hash);
        _worker.Script(Segment("Olá"), new WorkerEvent(WorkerEventKind.Done));
        await EnqueueAndWait(first, TranscriptionStatus.Done);

        await EnqueueAndWait(copy, TranscriptionStatus.Done);

        _worker.Jobs.Count.ShouldBe(1);
        (await _service.GetAsync(copy, Token)).Transcript!.Segments.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Status_ArquivoAlterado_VoltaANaoTranscrita()
    {
        var lesson = AddLesson("a.mp4");
        _worker.Script(new WorkerEvent(WorkerEventKind.Done));
        await EnqueueAndWait(lesson, TranscriptionStatus.Done);

        await UpdateLesson(lesson, entity => entity.FileSize = 999);

        (await _service.GetAsync(lesson, Token)).Status.ShouldBe(TranscriptionStatus.NotTranscribed);
    }

    [Fact]
    public async Task Status_PastaMovidaMesmoArquivo_MantemTranscricao()
    {
        var lesson = AddLesson("a.mp4");
        _worker.Script(Segment("Olá"), new WorkerEvent(WorkerEventKind.Done));
        await EnqueueAndWait(lesson, TranscriptionStatus.Done);

        await UpdateLesson(lesson, entity => entity.Path = _files.AddFile("nova/a.mp4"));

        (await _service.GetAsync(lesson, Token)).Status.ShouldBe(TranscriptionStatus.Done);
    }

    [Fact]
    public async Task Get_ComLegenda_ViraTranscricaoSemFila()
    {
        var lesson = AddLesson("a.mp4");
        _subtitles.Content = "1\n00:00:01,000 --> 00:00:02,000\nDa legenda\n"u8.ToArray();

        var result = await _service.GetAsync(lesson, Token);

        result.Status.ShouldBe(TranscriptionStatus.Done);
        result.Transcript!.Source.ShouldBe(TranscriptSource.Subtitle);
        result.Transcript.Segments.ShouldHaveSingleItem().Text.ShouldBe("Da legenda");
        _worker.Jobs.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_LegendaVenceOWhisper_MasAPreferenciaDoUsuarioFica()
    {
        var lesson = AddLesson("a.mp4");
        _worker.Script(Segment("Do Whisper"), new WorkerEvent(WorkerEventKind.Done));
        await EnqueueAndWait(lesson, TranscriptionStatus.Done);

        _subtitles.Content = "00:00:01.000 --> 00:00:02.000\nDa legenda\n"u8.ToArray();
        (await _service.GetAsync(lesson, Token)).Transcript!.Source.ShouldBe(TranscriptSource.Subtitle);

        await _service.EnqueueWithWhisperAsync(lesson, Token);
        await WaitForStatus(lesson, TranscriptionStatus.Done);
        (await _service.GetAsync(lesson, Token)).Transcript!.Source.ShouldBe(TranscriptSource.Whisper);
    }

    [Fact]
    public async Task Get_LegendaInvalida_ContinuaDisponivelParaOWhisper()
    {
        var lesson = AddLesson("a.mp4");
        _subtitles.Content = "isto não é legenda"u8.ToArray();

        (await _service.GetAsync(lesson, Token)).Status.ShouldBe(TranscriptionStatus.NotTranscribed);

        _logger.Entries.ShouldContain(entry => entry.Message.Contains("Legenda sem nenhum bloco", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Get_LegendaApagada_VoltaANaoTranscrita()
    {
        var lesson = AddLesson("a.mp4");
        _subtitles.Content = "00:00:01.000 --> 00:00:02.000\nDa legenda\n"u8.ToArray();
        await _service.GetAsync(lesson, Token);

        _subtitles.Content = null;

        (await _service.GetAsync(lesson, Token)).Status.ShouldBe(TranscriptionStatus.NotTranscribed);
    }

    [Fact]
    public async Task Enqueue_AulaComLegenda_NaoEntraNaFila()
    {
        var lesson = AddLesson("a.mp4");
        _subtitles.Content = "00:00:01.000 --> 00:00:02.000\nDa legenda\n"u8.ToArray();

        await _service.EnqueueAsync([lesson], Token);

        _worker.Jobs.ShouldBeEmpty();
    }

    [Fact]
    public async Task Logs_NaoContemTexto()
    {
        var lesson = AddLesson("a.mp4");
        _worker.Script(Segment("Texto secreto da aula"), new WorkerEvent(WorkerEventKind.Done));

        await EnqueueAndWait(lesson, TranscriptionStatus.Done);

        _logger.Entries.ShouldNotContain(entry => entry.Message.Contains("secreto", StringComparison.Ordinal));
    }

    private static WorkerEvent Segment(string text) => new(WorkerEventKind.Segment, StartMs: 1000, EndMs: 2000, Text: text);

    private TranscriptionService NewService() =>
        new(_store, _worker, _models, _subtitles, _hasher, _files, _settings, _time, _logger);

    private Guid AddLesson(string name, bool createFile = true)
    {
        var path = createFile ? _files.AddFile(name) : _files.PathOf(name);
        var courseId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        using var db = _database.CreateDbContext();
        db.Courses.Add(new CourseEntity { Id = courseId, Kind = CourseKind.Files, Title = name });
        db.Lessons.Add(new LessonEntity { Id = lessonId, CourseId = courseId, Path = path, Title = name, FileSize = 100, LastWriteUtc = DateTime.UnixEpoch });
        db.SaveChanges();
        return lessonId;
    }

    private async Task UpdateLesson(Guid lessonId, Action<LessonEntity> change)
    {
        await using var db = _database.CreateDbContext();
        change(db.Lessons.Single(lesson => lesson.Id == lessonId));
        await db.SaveChangesAsync(Token);
    }

    private async Task EnqueueAndWait(Guid lessonId, TranscriptionStatus status)
    {
        await _service.EnqueueAsync([lessonId], Token);
        await WaitForStatus(lessonId, status);
    }

    private Task WaitForStatus(Guid lessonId, TranscriptionStatus status) =>
        WaitUntil(async () => (await _store.GetRecordAsync(lessonId, Token))?.Status == status);

    private static async Task WaitUntil(Func<Task<bool>> condition)
    {
        var limit = DateTime.UtcNow + Timeout;
        while (!await condition())
        {
            DateTime.UtcNow.ShouldBeLessThan(limit, "condição não aconteceu a tempo");
            await Task.Delay(5, Token);
        }
    }

    /// <summary>
    /// Banco em memória com uma conexão por contexto, como em produção: o laço da fila e o teste
    /// usam o banco ao mesmo tempo, e isso quebraria numa conexão única.
    /// </summary>
    private sealed class ConcurrentMemoryDatabase : IDbContextFactory<VibePlayerDbContext>, IDisposable
    {
        private readonly string _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Guid.NewGuid().ToString(),
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 5,
        }.ToString();

        // O banco em memória vive enquanto houver uma conexão aberta.
        private readonly SqliteConnection _keepAlive;

        public ConcurrentMemoryDatabase()
        {
            _keepAlive = new SqliteConnection(_connectionString);
            _keepAlive.Open();
            using var db = CreateDbContext();
            db.Database.Migrate();
        }

        public VibePlayerDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<VibePlayerDbContext>().UseSqlite(_connectionString).Options);

        public void Dispose() => _keepAlive.Dispose();
    }

    private sealed class PhysicalSubtitleSourceFake : ISubtitleSource
    {
        public byte[]? Content { get; set; }

        public string? Find(string videoPath) => Content is null ? null : videoPath + ".srt";

        public Task<byte[]> ReadAsync(string subtitlePath, CancellationToken cancellationToken = default) => Task.FromResult(Content!);
    }

    private sealed class Gate
    {
        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Channel<WorkerEvent> _events = Channel.CreateUnbounded<WorkerEvent>();

        public Task Reached => _reached.Task;

        public ChannelReader<WorkerEvent> Events => _events.Reader;

        public void MarkReached() => _reached.TrySetResult();

        public void Release(WorkerEvent workerEvent)
        {
            _events.Writer.TryWrite(workerEvent);
            _events.Writer.TryComplete();
        }
    }

    private sealed class ScriptedWorker : ITranscriptionWorker
    {
        private readonly Queue<(IReadOnlyList<WorkerEvent> Events, int? ExitCode, Gate? Gate)> _scripts = new();

        public List<TranscriptionJob> Jobs { get; } = [];

        public void Script(params WorkerEvent[] events) => _scripts.Enqueue((events, null, null));

        public void ScriptCrash(int exitCode, params WorkerEvent[] events) => _scripts.Enqueue((events, exitCode, null));

        public Gate ScriptGated(params WorkerEvent[] events)
        {
            var gate = new Gate();
            _scripts.Enqueue((events, null, gate));
            return gate;
        }

        public async IAsyncEnumerable<WorkerEvent> RunAsync(TranscriptionJob job, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Jobs.Add(job);
            var (events, exitCode, gate) = _scripts.Dequeue();
            foreach (var workerEvent in events)
            {
                yield return workerEvent;
            }

            if (gate is not null)
            {
                gate.MarkReached();
                await foreach (var workerEvent in gate.Events.ReadAllAsync(cancellationToken))
                {
                    yield return workerEvent;
                }
            }

            if (exitCode is { } code)
            {
                throw new WorkerCrashedException(code);
            }
        }
    }
}
