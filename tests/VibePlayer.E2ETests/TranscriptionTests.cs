using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using VibePlayer.App.Resources;
using VibePlayer.App.Services;
using VibePlayer.App.ViewModels;
using VibePlayer.App.Views;
using VibePlayer.App.Views.Assistant;
using VibePlayer.Core.Library;
using VibePlayer.Core.Playback;
using VibePlayer.Core.Transcription;

namespace VibePlayer.E2ETests;

/// <summary>A aba Transcrição com a janela e o banco reais; só o processo do Whisper é falso.</summary>
public sealed class TranscriptionTests : IDisposable
{
    private const string Lesson = "1 - Aula";
    private const string SecretText = "conteudo-da-aula-que-nao-vai-para-o-log";

    private readonly PlayerHarness _app = new();
    private readonly FakeTranscriptionWorker _worker = new();
    private readonly FakeModelStore _models = new();
    private readonly ScriptedDialogs _dialogs = new();

    public TranscriptionTests()
    {
        File.WriteAllText(_app.Sample.AddVideo($"Curso/{Lesson}.mp4"), "conteúdo do primeiro vídeo");
        File.WriteAllText(_app.Sample.AddVideo("Curso/2 - Aula.mp4"), "conteúdo do segundo vídeo");
    }

    public void Dispose() => _app.Dispose();

    [AvaloniaFact]
    public async Task Estados_SemAula_MostraOrientacao()
    {
        var window = await OpenAsync();

        window.ViewModel().Transcription.State.ShouldBe(TranscriptionViewState.NoLesson);
        ControlIn(window, "NoLessonState").IsEffectivelyVisible.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public async Task Estados_CadaUm_MostraOTextoCerto()
    {
        var window = await OpenWithLessonAsync();
        var viewModel = window.ViewModel().Transcription;
        ControlIn(window, "NotTranscribedState").IsEffectivelyVisible.ShouldBeTrue();

        var gate = _worker.ScriptGated(
            new WorkerEvent(WorkerEventKind.Stage, TranscriptionStage.Transcribing),
            new WorkerEvent(WorkerEventKind.Progress, Fraction: 0.4));
        await viewModel.TranscribeCommand.ExecuteAsync(null);
        await PlayerHarness.WaitUntilAsync(() => viewModel.ProgressPercent > 39, "progresso");
        ControlIn(window, "WorkingState").IsEffectivelyVisible.ShouldBeTrue();
        ((TextBlock)ControlIn(window, "StageLabel")).Text.ShouldBe(Strings.Transcription_StageTranscribing);
        ((ProgressBar)ControlIn(window, "Progress")).Value.ShouldBe(40, 0.5);

        gate.Finish(new WorkerEvent(WorkerEventKind.Error, Value: TranscriptionErrors.UnreadableFile));
        await PlayerHarness.WaitUntilAsync(() => viewModel.State == TranscriptionViewState.Failed, "falha");
        WindowTestExtensions.Settle();
        ((TextBlock)ControlIn(window, "FailedMessage")).Text.ShouldBe(Strings.Transcription_ErrorUnreadable);

        _worker.Script(new WorkerEvent(WorkerEventKind.NoAudio));
        await viewModel.TranscribeCommand.ExecuteAsync(null);
        await PlayerHarness.WaitUntilAsync(() => viewModel.State == TranscriptionViewState.NoAudio, "sem áudio");
        WindowTestExtensions.Settle();
        ControlIn(window, "NoAudioState").IsEffectivelyVisible.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public async Task Cancelar_VoltaANaoTranscrita()
    {
        var window = await OpenWithLessonAsync();
        var viewModel = window.ViewModel().Transcription;
        var gate = _worker.ScriptGated(new WorkerEvent(WorkerEventKind.Stage, TranscriptionStage.Extracting));
        await viewModel.TranscribeCommand.ExecuteAsync(null);
        await gate.Reached.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await viewModel.CancelCommand.ExecuteAsync(null);

        await PlayerHarness.WaitUntilAsync(() => viewModel.State == TranscriptionViewState.NotTranscribed, "cancelada");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Tempo_Clique_LevaOVideoAoPonto()
    {
        var window = await OpenWithLessonAsync();
        var viewModel = window.ViewModel().Transcription;
        _worker.Script(Segment(95, "Primeiro trecho"), new WorkerEvent(WorkerEventKind.Done, Value: "pt"));
        await viewModel.TranscribeCommand.ExecuteAsync(null);
        await PlayerHarness.WaitUntilAsync(() => viewModel.Segments.Count == 1, "texto");
        await PlayerHarness.SettleAsync();

        var jump = ControlIn(window, "SegmentList").GetVisualDescendants().OfType<Button>().First();
        jump.Command!.Execute(jump.CommandParameter);

        _app.Player.Position.ShouldBe(TimeSpan.FromSeconds(95));
        viewModel.Segments[0].Time.ShouldBe("1:35");
        viewModel.SourceText.ShouldNotBeNull().ShouldContain("Whisper");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Aula_ComLegenda_MostraSemWhisper()
    {
        File.WriteAllText(_app.Sample.PathOf($"Curso/{Lesson}.srt"), "1\n00:00:10,000 --> 00:00:12,000\nTexto da legenda\n");

        var window = await OpenWithLessonAsync();
        var viewModel = window.ViewModel().Transcription;

        await PlayerHarness.WaitUntilAsync(() => viewModel.State == TranscriptionViewState.Done, "legenda");
        viewModel.Segments.ShouldHaveSingleItem().Text.ShouldBe("Texto da legenda");
        viewModel.IsFromSubtitle.ShouldBeTrue();
        _worker.Jobs.ShouldBeEmpty();
        _dialogs.ModelQuestions.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public async Task PrimeiroUso_SemModelo_PedeConfirmacaoEBaixa()
    {
        _models.Available = false;
        _worker.Script(new WorkerEvent(WorkerEventKind.Done));
        var window = await OpenWithLessonAsync();
        var viewModel = window.ViewModel().Transcription;

        await viewModel.TranscribeCommand.ExecuteAsync(null);

        _dialogs.ModelQuestions.ShouldBe(1);
        _models.Downloads.ShouldBe([WhisperModels.SmallQuantized]);
        await PlayerHarness.WaitUntilAsync(() => viewModel.State == TranscriptionViewState.Done, "transcrita depois do download");
        window.Close();
    }

    [AvaloniaFact]
    public async Task PrimeiroUso_UsuarioRecusa_NaoBaixaNemTranscreve()
    {
        _models.Available = false;
        _dialogs.ModelAnswer = null;
        var window = await OpenWithLessonAsync();

        await window.ViewModel().Transcription.TranscribeCommand.ExecuteAsync(null);

        _models.Downloads.ShouldBeEmpty();
        _worker.Jobs.ShouldBeEmpty();
        window.Close();
    }

    [AvaloniaFact]
    public async Task Reabertura_AulasPendentes_ContinuamNaFila()
    {
        var window = await OpenWithLessonAsync();
        var gate = _worker.ScriptGated();
        await window.ViewModel().Transcription.TranscribeCommand.ExecuteAsync(null);
        await gate.Reached.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        window.Close();

        // Fecha o container antigo, como fechar o app no meio da transcrição.
        await _app.Services.DisposeAsync();
        _worker.Script(new WorkerEvent(WorkerEventKind.Done));
        var reopened = await OpenAsync();

        await PlayerHarness.WaitUntilAsync(() => _worker.Jobs.Count == 2, "a aula recomeçou");
        await PlayerHarness.SelectAsync(reopened, Lesson);
        await PlayerHarness.WaitUntilAsync(() => reopened.ViewModel().Transcription.State == TranscriptionViewState.Done, "concluída");
        reopened.Close();
    }

    [AvaloniaFact]
    public async Task Reconectar_PastaMovida_MantemTranscricao()
    {
        var window = await OpenWithLessonAsync();
        var viewModel = window.ViewModel().Transcription;
        _worker.Script(Segment(1, "Texto"), new WorkerEvent(WorkerEventKind.Done));
        await viewModel.TranscribeCommand.ExecuteAsync(null);
        await PlayerHarness.WaitUntilAsync(() => viewModel.State == TranscriptionViewState.Done, "transcrita");

        var library = _app.Services.GetRequiredService<ICourseLibrary>();
        var root = (await library.GetCoursesAsync(TestContext.Current.CancellationToken)).Single().RootId!.Value;
        Directory.Move(_app.Sample.PathOf("Curso"), _app.Sample.PathOf("Movida"));
        await library.RelocateRootAsync(root, _app.Sample.PathOf("Movida"), TestContext.Current.CancellationToken);
        await library.RefreshAsync(cancellationToken: TestContext.Current.CancellationToken);

        await viewModel.ShowLessonAsync(PlayerHarness.Lesson(window, Lesson).Id);

        viewModel.State.ShouldBe(TranscriptionViewState.Done);
        _worker.Jobs.Count.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public async Task TranscreverCurso_PeloMenu_PoeTodasNaFilaEAbreAAba()
    {
        var window = await OpenAsync();
        await _app.AddFolderAsync("Curso");
        await window.ViewModel().Library.RefreshCommand.ExecuteAsync(null);
        await PlayerHarness.SettleAsync();
        _worker.Script(new WorkerEvent(WorkerEventKind.Done));
        _worker.Script(new WorkerEvent(WorkerEventKind.Done));

        window.ViewModel().Library.TranscribeCommand.Execute(window.ViewModel().Library.Courses.Single());

        await PlayerHarness.WaitUntilAsync(() => _worker.Jobs.Count == 2, "as duas aulas");
        window.ViewModel().SelectedTab.ShouldBe(SidePanelTab.Transcription);
        window.Close();
    }

    [AvaloniaFact]
    public async Task VlcAusente_ComLegenda_Funciona()
    {
        File.WriteAllText(_app.Sample.PathOf($"Curso/{Lesson}.vtt"), "WEBVTT\n\n00:01.000 --> 00:02.000\nDa legenda\n");
        var window = await OpenAsync();
        _app.Player.Availability = PlayerAvailability.Unavailable;
        var viewModel = window.ViewModel().Transcription;
        await AddCourseAsync(window);

        await viewModel.ShowLessonAsync(PlayerHarness.Lesson(window, Lesson).Id);

        viewModel.State.ShouldBe(TranscriptionViewState.Done);
        window.Close();
    }

    [AvaloniaFact]
    public async Task VlcAusente_SemLegenda_Explica()
    {
        var window = await OpenAsync();
        _app.Player.Availability = PlayerAvailability.Unavailable;
        var viewModel = window.ViewModel().Transcription;
        await AddCourseAsync(window);

        await viewModel.ShowLessonAsync(PlayerHarness.Lesson(window, Lesson).Id);
        WindowTestExtensions.Settle();

        viewModel.State.ShouldBe(TranscriptionViewState.VlcMissing);
        window.ViewModel().SelectedTab = SidePanelTab.Transcription;
        WindowTestExtensions.Settle();
        ControlIn(window, "VlcMissingState").IsEffectivelyVisible.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public async Task Logs_NaoContemTexto()
    {
        var window = await OpenWithLessonAsync();
        var viewModel = window.ViewModel().Transcription;
        _worker.Script(Segment(1, SecretText), new WorkerEvent(WorkerEventKind.Done));

        await viewModel.TranscribeCommand.ExecuteAsync(null);
        await PlayerHarness.WaitUntilAsync(() => viewModel.State == TranscriptionViewState.Done, "transcrita");

        TestAppBuilder.LogSink.Events.ShouldNotContain(entry => entry.RenderMessage(null).Contains(SecretText, StringComparison.Ordinal));
        window.Close();
    }

    private static WorkerEvent Segment(int seconds, string text) =>
        new(WorkerEventKind.Segment, StartMs: seconds * 1000L, EndMs: (seconds + 2) * 1000L, Text: text);

    private static Control ControlIn(MainWindow window, string name)
    {
        window.ViewModel().SelectedTab = SidePanelTab.Transcription;
        WindowTestExtensions.Settle();
        var view = window.GetVisualDescendants().OfType<TranscriptionView>().Single();
        return view.GetVisualDescendants().OfType<Control>().First(control => control.Name == name);
    }

    private Task<MainWindow> OpenAsync() => _app.OpenAsync(services =>
    {
        services.AddSingleton<ITranscriptionWorker>(_worker);
        services.AddSingleton<IWhisperModelStore>(_models);
        services.AddSingleton<IDialogService>(_dialogs);
    });

    private async Task AddCourseAsync(MainWindow window)
    {
        await _app.AddFolderAsync("Curso");
        await window.ViewModel().Library.RefreshCommand.ExecuteAsync(null);
        await PlayerHarness.SettleAsync();
    }

    private async Task<MainWindow> OpenWithLessonAsync()
    {
        var window = await OpenAsync();
        await AddCourseAsync(window);
        await PlayerHarness.SelectAsync(window, Lesson);
        window.ViewModel().SelectedTab = SidePanelTab.Transcription;
        await PlayerHarness.SettleAsync();
        return window;
    }

    private sealed class FakeModelStore : IWhisperModelStore
    {
        public bool Available { get; set; } = true;

        public List<WhisperModel> Downloads { get; } = [];

        public bool IsAvailable(WhisperModel model) => Available;

        public string PathOf(WhisperModel model) => "/modelos/" + model.FileName;

        public Task DownloadAsync(WhisperModel model, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            Downloads.Add(model);
            Available = true;
            return Task.CompletedTask;
        }
    }

    private sealed class Gate
    {
        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Channel<WorkerEvent> _events = Channel.CreateUnbounded<WorkerEvent>();

        public Task Reached => _reached.Task;

        public ChannelReader<WorkerEvent> Events => _events.Reader;

        public void MarkReached() => _reached.TrySetResult();

        public void Finish(WorkerEvent workerEvent)
        {
            _events.Writer.TryWrite(workerEvent);
            _events.Writer.TryComplete();
        }
    }

    private sealed class FakeTranscriptionWorker : ITranscriptionWorker
    {
        private readonly Queue<(IReadOnlyList<WorkerEvent> Events, Gate? Gate)> _scripts = new();

        public List<TranscriptionJob> Jobs { get; } = [];

        public void Script(params WorkerEvent[] events) => _scripts.Enqueue((events, null));

        public Gate ScriptGated(params WorkerEvent[] events)
        {
            var gate = new Gate();
            _scripts.Enqueue((events, gate));
            return gate;
        }

        public async IAsyncEnumerable<WorkerEvent> RunAsync(TranscriptionJob job, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Jobs.Add(job);
            var (events, gate) = _scripts.Dequeue();
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
        }
    }
}
