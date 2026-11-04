using System.Diagnostics;
using VibePlayer.Core.Transcription;
using VibePlayer.Infrastructure.Transcription;

namespace VibePlayer.E2ETests;

/// <summary>
/// O processo separado: morte, travamento e cancelamento, com um programa de teste que faz isso
/// sob comando (tests/VibePlayer.WorkerStub).
/// </summary>
[Collection("VLC real")]
public sealed class ProcessTranscriptionWorkerTests
{
    private static readonly TranscriptionJob Job = new("/aula.mp4", "/m.bin", "auto", 1, true);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Run_ProcessoMorre_InformaFalhaComCodigoDeSaida()
    {
        var error = await Should.ThrowAsync<WorkerCrashedException>(() => Collect("crash"));

        error.ExitCode.ShouldBe(3);
    }

    [Fact]
    public async Task Run_LinhaEstranhaNaSaida_Ignora()
    {
        var events = await Collect("noise");

        events.ShouldHaveSingleItem().Kind.ShouldBe(WorkerEventKind.Done);
    }

    [Fact]
    public async Task Run_ProcessoDemoraASair_MantemOResultado()
    {
        var events = await Collect("slow-exit");

        events.ShouldHaveSingleItem().Kind.ShouldBe(WorkerEventKind.Done);
    }

    [Fact]
    public async Task Cancel_ProcessoEmAndamento_TerminaEmDoisSegundos()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var stopwatch = Stopwatch.StartNew();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var workerEvent in Stub("hang").RunAsync(Job, cancellation.Token))
            {
                workerEvent.Kind.ShouldBe(WorkerEventKind.Progress);
                stopwatch.Restart();
                await cancellation.CancelAsync();
            }
        });

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    private static async Task<List<WorkerEvent>> Collect(string mode)
    {
        var events = new List<WorkerEvent>();
        await foreach (var workerEvent in Stub(mode).RunAsync(Job, Token))
        {
            events.Add(workerEvent);
        }

        return events;
    }

    private static ProcessTranscriptionWorker Stub(string mode)
    {
        // Mesma configuração de build dos testes: troca só o nome do projeto no caminho.
        var dll = Path.Combine(
            AppContext.BaseDirectory.Replace("VibePlayer.E2ETests", "VibePlayer.WorkerStub", StringComparison.Ordinal),
            "VibePlayer.WorkerStub.dll");
        File.Exists(dll).ShouldBeTrue(dll);
        return new ProcessTranscriptionWorker(new TranscriptionWorkerCommand("dotnet", [dll, mode]));
    }
}

/// <summary>
/// O executável real do app como processo de transcrição, com VLC e Whisper de verdade. O modelo
/// vem de VIBEPLAYER_WHISPER_MODEL (a CI baixa o tiny); sem ele, os testes do Whisper são pulados.
/// </summary>
[Collection("VLC real")]
public sealed class WhisperIntegrationTests
{
    private static readonly string? Model = Environment.GetEnvironmentVariable("VIBEPLAYER_WHISPER_MODEL");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Transcribe_FalaDeDominioPublico_TrazPalavrasESegmentosEmOrdem()
    {
        Assert.SkipWhen(string.IsNullOrEmpty(Model), "VIBEPLAYER_WHISPER_MODEL não definido");

        var events = await RunAsync(new TranscriptionJob(Fixture("audio/fala-jfk.m4a"), Model!, "en", 2, ForceCpu: false));

        events[^1].Kind.ShouldBe(WorkerEventKind.Done);
        events[^1].Value.ShouldBe("en");
        events.ShouldContain(e => e.Kind == WorkerEventKind.Runtime);
        var segments = events.Where(e => e.Kind == WorkerEventKind.Segment).ToList();
        string.Concat(segments.Select(s => s.Text)).ShouldContain("country", Case.Insensitive);
        segments.Select(s => s.StartMs).ShouldBeInOrder();
        segments.ShouldAllBe(s => s.EndMs >= s.StartMs);
    }

    [Fact]
    public async Task Transcribe_ForcandoCpu_UsaACpu()
    {
        Assert.SkipWhen(string.IsNullOrEmpty(Model), "VIBEPLAYER_WHISPER_MODEL não definido");

        var events = await RunAsync(new TranscriptionJob(Fixture("audio/fala-jfk.m4a"), Model!, "en", 2, ForceCpu: true));

        events.First(e => e.Kind == WorkerEventKind.Runtime).Value.ShouldStartWith("Cpu");
        events[^1].Kind.ShouldBe(WorkerEventKind.Done);
    }

    [Fact]
    public async Task Run_ModeloCorrompido_InformaOCodigo()
    {
        var model = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await File.WriteAllBytesAsync(model, new byte[4096], Token);

        var events = await RunAsync(new TranscriptionJob(Fixture("audio/fala-jfk.m4a"), model, "en", 1, ForceCpu: true));

        File.Delete(model);
        events[^1].ShouldBe(new WorkerEvent(WorkerEventKind.Error, Value: TranscriptionErrors.ModelInvalid));
    }

    [Fact]
    public async Task Run_SemAudio_Informa()
    {
        var events = await RunAsync(new TranscriptionJob(Fixture("audio/sem-audio.mp4"), "/nao/existe.bin", "en", 1, ForceCpu: true));

        events[^1].Kind.ShouldBe(WorkerEventKind.NoAudio);
    }

    [Fact]
    public async Task Run_ArquivoCorrompido_InformaOCodigo()
    {
        var events = await RunAsync(new TranscriptionJob(Fixture("videos/corrompido.mp4"), "/nao/existe.bin", "en", 1, ForceCpu: true));

        events[^1].Value.ShouldBe(TranscriptionErrors.UnreadableFile);
    }

    private static async Task<List<WorkerEvent>> RunAsync(TranscriptionJob job)
    {
        var worker = new ProcessTranscriptionWorker(new TranscriptionWorkerCommand(AppExecutable(), [WorkerProtocol.Argument]));
        var events = new List<WorkerEvent>();
        await foreach (var workerEvent in worker.RunAsync(job, Token))
        {
            events.Add(workerEvent);
        }

        return events;
    }

    private static string AppExecutable()
    {
        var root = RepositoryRoot();
        var testBin = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var name = OperatingSystem.IsWindows() ? "VibePlayer.exe" : "VibePlayer";
        var path = Path.Combine(root, "src", "VibePlayer.App", "bin", testBin.Parent!.Name, testBin.Name, name);
        File.Exists(path).ShouldBeTrue(path);
        return path;
    }

    private static string Fixture(string relativePath) => Path.GetFullPath(Path.Combine(RepositoryRoot(), "tests", "fixtures", relativePath));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VibePlayer.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull();
        return directory.FullName;
    }
}
