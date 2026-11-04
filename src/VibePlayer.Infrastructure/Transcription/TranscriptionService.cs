using Microsoft.Extensions.Logging;
using VibePlayer.Core.Library;
using VibePlayer.Core.Settings;
using VibePlayer.Core.Threading;
using VibePlayer.Core.Transcription;

namespace VibePlayer.Infrastructure.Transcription;

/// <summary>
/// Legendas, cache e fila do Whisper.
/// </summary>
/// <remarks>
/// A fila fica no banco (status <c>Queued</c>) para sobreviver a fechar o app; em memória há só a
/// ordem. Um único laço em segundo plano trata uma aula por vez: duas ao mesmo tempo disputariam
/// os mesmos núcleos sem terminar antes, e somariam gigabytes de memória.
///
/// O hash é calculado dentro do laço, e não ao pedir: um vídeo grande em disco externo leva
/// segundos, e o clique em "Transcrever curso" não pode esperar por todos.
///
/// Os segmentos só chegam ao banco no fim, numa transação. Recomeçar uma aula interrompida é mais
/// simples e mais seguro do que emendar segmentos na fronteira do corte.
/// </remarks>
public sealed partial class TranscriptionService(
    ITranscriptStore store,
    ITranscriptionWorker worker,
    IWhisperModelStore models,
    ISubtitleSource subtitles,
    IFileHasher hasher,
    IFileSystemReader fileSystem,
    ISettingsStore settings,
    TimeProvider timeProvider,
    ILogger<TranscriptionService> logger) : ITranscriptionService, IDisposable
{
    private readonly Lock _lock = new();
    private readonly LinkedList<Guid> _queue = [];
    private readonly CancellationTokenSource _lifetime = new();
    private Guid? _running;
    private Guid? _current;
    private CancellationTokenSource? _runningCancellation;
    private bool _loopActive;

    public event EventHandler<Guid>? Changed;

    public event EventHandler<TranscriptionProgress>? ProgressChanged;

    public async Task<LessonTranscription> GetAsync(Guid lessonId, CancellationToken cancellationToken = default)
    {
        var file = await store.GetLessonFileAsync(lessonId, cancellationToken).ConfigureAwait(false);
        if (file is null)
        {
            return new LessonTranscription(lessonId, TranscriptionStatus.NotTranscribed);
        }

        var record = await store.GetRecordAsync(lessonId, cancellationToken).ConfigureAwait(false);
        if (record?.Status is TranscriptionStatus.Queued or TranscriptionStatus.Running)
        {
            return new LessonTranscription(lessonId, record.Status);
        }

        if (record is not { PreferWhisper: true }
            && await LinkSubtitleAsync(file, record, cancellationToken).ConfigureAwait(false) is { } fromSubtitle)
        {
            return fromSubtitle;
        }

        return record is null
            ? new LessonTranscription(lessonId, TranscriptionStatus.NotTranscribed)
            : await ResolveSavedAsync(file, record, cancellationToken).ConfigureAwait(false);
    }

    public async Task EnqueueAsync(IReadOnlyList<Guid> lessonIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lessonIds);
        foreach (var lessonId in lessonIds)
        {
            // GetAsync primeiro: uma aula com legenda, em "Transcrever curso", não vai para a fila.
            var current = await GetAsync(lessonId, cancellationToken).ConfigureAwait(false);
            if (current.Status is TranscriptionStatus.NotTranscribed or TranscriptionStatus.Failed)
            {
                var record = await store.GetRecordAsync(lessonId, cancellationToken).ConfigureAwait(false);
                await AddToQueueAsync(lessonId, record, record?.PreferWhisper ?? false, cancellationToken).ConfigureAwait(false);
            }
        }

        StartLoop();
    }

    public async Task EnqueueWithWhisperAsync(Guid lessonId, CancellationToken cancellationToken = default)
    {
        var record = await store.GetRecordAsync(lessonId, cancellationToken).ConfigureAwait(false);
        await AddToQueueAsync(lessonId, record, preferWhisper: true, cancellationToken).ConfigureAwait(false);
        StartLoop();
    }

    public async Task CancelAsync(Guid lessonId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _queue.Remove(lessonId);
            if (_running == lessonId)
            {
                // O laço apaga o vínculo ao ver o cancelamento, com o processo já morto.
                _runningCancellation?.Cancel();
                return;
            }
        }

        await store.DeleteRecordAsync(lessonId, cancellationToken).ConfigureAwait(false);
        Changed?.Invoke(this, lessonId);
    }

    public void SetCurrentLesson(Guid? lessonId)
    {
        lock (_lock)
        {
            _current = lessonId;
        }
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        var pending = await store.GetPendingAsync(cancellationToken).ConfigureAwait(false);
        foreach (var record in pending)
        {
            // "Running" ao abrir só existe se o app fechou no meio: a aula recomeça.
            if (record.Status == TranscriptionStatus.Running)
            {
                await store.SaveRecordAsync(record with { Status = TranscriptionStatus.Queued }, cancellationToken).ConfigureAwait(false);
            }

            lock (_lock)
            {
                _queue.AddLast(record.LessonId);
            }
        }

        StartLoop();
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private async Task AddToQueueAsync(Guid lessonId, LessonTranscriptionRecord? record, bool preferWhisper, CancellationToken cancellationToken)
    {
        var queued = (record ?? new LessonTranscriptionRecord(lessonId, TranscriptionStatus.Queued)) with
        {
            Status = TranscriptionStatus.Queued,
            ErrorCode = null,
            TranscriptId = null,
            QueuedAt = timeProvider.GetUtcNow(),
            PreferWhisper = preferWhisper,
        };
        await store.SaveRecordAsync(queued, cancellationToken).ConfigureAwait(false);
        lock (_lock)
        {
            if (!_queue.Contains(lessonId) && _running != lessonId)
            {
                _queue.AddLast(lessonId);
            }
        }

        Changed?.Invoke(this, lessonId);
    }

    private async Task<LessonTranscription?> LinkSubtitleAsync(LessonFile file, LessonTranscriptionRecord? record, CancellationToken cancellationToken)
    {
        byte[] content;
        string? path;
        try
        {
            path = subtitles.Find(file.Path);
            if (path is null)
            {
                return null;
            }

            content = await subtitles.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSubtitleUnreadable(file.LessonId);
            return null;
        }

        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content));
        var transcriptId = await store.FindTranscriptAsync(hash, TranscriptSource.Subtitle, cancellationToken).ConfigureAwait(false);
        if (transcriptId is null)
        {
            var segments = SubtitleParser.Parse(SubtitleEncoding.Decode(content));
            if (segments.Count == 0)
            {
                LogSubtitleInvalid(file.LessonId);
                return null;
            }

            transcriptId = Guid.NewGuid();
            var created = new Transcript(transcriptId.Value, TranscriptSource.Subtitle, null, null, segments);
            await store.SaveTranscriptAsync(hash, created, cancellationToken).ConfigureAwait(false);
        }

        if (record?.TranscriptId != transcriptId || record.Status != TranscriptionStatus.Done)
        {
            var linked = new LessonTranscriptionRecord(file.LessonId, TranscriptionStatus.Done, transcriptId);
            await store.SaveRecordAsync(linked, cancellationToken).ConfigureAwait(false);
        }

        var transcript = await store.GetTranscriptAsync(transcriptId.Value, cancellationToken).ConfigureAwait(false);
        return new LessonTranscription(file.LessonId, TranscriptionStatus.Done, Transcript: transcript);
    }

    private async Task<LessonTranscription> ResolveSavedAsync(LessonFile file, LessonTranscriptionRecord record, CancellationToken cancellationToken)
    {
        // Vídeo trocado: tamanho ou data diferentes de quando o hash foi calculado.
        var fileChanged = record.HashedFileSize is { } size && (size != file.FileSize || record.HashedLastWriteUtc != file.LastWriteUtc);
        var transcript = record.TranscriptId is { } id
            ? await store.GetTranscriptAsync(id, cancellationToken).ConfigureAwait(false)
            : null;

        // A legenda que originou a transcrição sumiu: o texto deixa de valer para esta aula.
        var subtitleGone = transcript?.Source == TranscriptSource.Subtitle;
        var doneWithoutText = record.Status == TranscriptionStatus.Done && transcript is null;
        if (fileChanged || subtitleGone || doneWithoutText)
        {
            await store.DeleteRecordAsync(file.LessonId, cancellationToken).ConfigureAwait(false);
            return new LessonTranscription(file.LessonId, TranscriptionStatus.NotTranscribed);
        }

        return new LessonTranscription(file.LessonId, record.Status, record.ErrorCode, transcript);
    }

    private void StartLoop()
    {
        lock (_lock)
        {
            if (_loopActive || _queue.Count == 0)
            {
                return;
            }

            _loopActive = true;
        }

        RunLoopAsync().FireAndForget(logger, "TranscriptionQueue");
    }

    private async Task RunLoopAsync()
    {
        // Sai da thread de quem pediu antes do primeiro trabalho pesado.
        await Task.Yield();
        while (TakeNext() is { } lessonId)
        {
            try
            {
                await ProcessAsync(lessonId).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Uma aula com problema não pode parar as seguintes.
                LogUnexpected(ex, lessonId);
                await FailAsync(lessonId, TranscriptionErrors.Unexpected).ConfigureAwait(false);
            }
            finally
            {
                FinishRunning();
            }

            Changed?.Invoke(this, lessonId);
        }
    }

    private Guid? TakeNext()
    {
        lock (_lock)
        {
            if (_queue.Count == 0 || _lifetime.IsCancellationRequested)
            {
                _loopActive = false;
                return null;
            }

            // A aula aberta passa à frente: é a que o usuário quer agora.
            var next = _current is { } current && _queue.Contains(current) ? current : _queue.First!.Value;
            _queue.Remove(next);
            _running = next;
            _runningCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            return next;
        }
    }

    private void FinishRunning()
    {
        lock (_lock)
        {
            _running = null;
            _runningCancellation?.Dispose();
            _runningCancellation = null;
        }
    }

    private async Task ProcessAsync(Guid lessonId)
    {
        CancellationToken token;
        lock (_lock)
        {
            token = _runningCancellation!.Token;
        }

        try
        {
            await ProcessCoreAsync(lessonId, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!_lifetime.IsCancellationRequested)
        {
            await store.DeleteRecordAsync(lessonId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // App fechando: o status fica "Running" e ResumeAsync recomeça a aula na reabertura.
        }
    }

    private async Task ProcessCoreAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        var record = await store.GetRecordAsync(lessonId, cancellationToken).ConfigureAwait(false);
        var file = await store.GetLessonFileAsync(lessonId, cancellationToken).ConfigureAwait(false);
        if (record is null || file is null)
        {
            return;
        }

        record = record with { Status = TranscriptionStatus.Running };
        await store.SaveRecordAsync(record, cancellationToken).ConfigureAwait(false);
        Changed?.Invoke(this, lessonId);
        ProgressChanged?.Invoke(this, new TranscriptionProgress(lessonId, TranscriptionStage.Preparing, 0));

        if (!file.IsAvailable || fileSystem.GetFile(file.Path) is null)
        {
            await FailAsync(lessonId, TranscriptionErrors.FileMissing).ConfigureAwait(false);
            return;
        }

        var hash = record.HashedFileSize == file.FileSize && record.HashedLastWriteUtc == file.LastWriteUtc && record.VideoHash is { } saved
            ? saved
            : await hasher.ComputeAsync(file.Path, cancellationToken).ConfigureAwait(false);
        record = record with { VideoHash = hash, HashedFileSize = file.FileSize, HashedLastWriteUtc = file.LastWriteUtc };

        if (await store.FindTranscriptAsync(hash, TranscriptSource.Whisper, cancellationToken).ConfigureAwait(false) is { } cached)
        {
            await store.SaveRecordAsync(record with { Status = TranscriptionStatus.Done, TranscriptId = cached }, cancellationToken).ConfigureAwait(false);
            return;
        }

        var model = WhisperModels.ByName(await settings.GetAsync(TranscriptionSettings.Model, cancellationToken).ConfigureAwait(false));
        if (!models.IsAvailable(model))
        {
            await store.SaveRecordAsync(record with { Status = TranscriptionStatus.Failed, ErrorCode = TranscriptionErrors.ModelMissing }, cancellationToken).ConfigureAwait(false);
            return;
        }

        var outcome = await RunWorkerWithFallbackAsync(lessonId, file, model, cancellationToken).ConfigureAwait(false);
        await SaveOutcomeAsync(record, hash, model, outcome, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkerOutcome> RunWorkerWithFallbackAsync(Guid lessonId, LessonFile file, WhisperModel model, CancellationToken cancellationToken)
    {
        var language = await settings.GetAsync(TranscriptionSettings.Language, cancellationToken).ConfigureAwait(false);
        var forceCpu = await settings.GetAsync(TranscriptionSettings.ForceCpu, cancellationToken).ConfigureAwait(false);

        // Metade dos processadores lógicos, até 8: acima disso o ganho medido cai, e o resto fica
        // para o player. Com GPU, o valor quase não pesa.
        var threads = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);
        var job = new TranscriptionJob(file.Path, models.PathOf(model), language, threads, forceCpu);

        var outcome = await RunWorkerAsync(lessonId, job, cancellationToken).ConfigureAwait(false);
        if (outcome.CrashedOnGpu)
        {
            LogGpuFallback(lessonId);
            await settings.SetAsync(TranscriptionSettings.ForceCpu, true, cancellationToken).ConfigureAwait(false);
            outcome = await RunWorkerAsync(lessonId, job with { ForceCpu = true }, cancellationToken).ConfigureAwait(false);
        }

        return outcome;
    }

    private async Task<WorkerOutcome> RunWorkerAsync(Guid lessonId, TranscriptionJob job, CancellationToken cancellationToken)
    {
        var segments = new List<TranscriptSegment>();
        var stage = TranscriptionStage.Extracting;
        string? runtime = null;
        var started = timeProvider.GetTimestamp();
        try
        {
            await foreach (var workerEvent in worker.RunAsync(job, cancellationToken).ConfigureAwait(false))
            {
                switch (workerEvent.Kind)
                {
                    case WorkerEventKind.Stage:
                        stage = workerEvent.Stage ?? stage;
                        ProgressChanged?.Invoke(this, new TranscriptionProgress(lessonId, stage, 0));
                        break;
                    case WorkerEventKind.Progress:
                        ProgressChanged?.Invoke(this, new TranscriptionProgress(lessonId, stage, workerEvent.Fraction ?? 0));
                        break;
                    case WorkerEventKind.Runtime:
                        runtime = workerEvent.Value;
                        break;
                    case WorkerEventKind.Segment:
                        segments.Add(new TranscriptSegment(
                            TimeSpan.FromMilliseconds(workerEvent.StartMs ?? 0),
                            TimeSpan.FromMilliseconds(workerEvent.EndMs ?? 0),
                            workerEvent.Text ?? string.Empty));
                        break;
                    case WorkerEventKind.LowQualityAudio:
                        LogLowQualityAudio(lessonId);
                        break;
                    case WorkerEventKind.NoAudio:
                        return new WorkerOutcome(NoAudio: true);
                    case WorkerEventKind.Error:
                        return new WorkerOutcome(ErrorCode: workerEvent.Value ?? TranscriptionErrors.Unexpected);
                    case WorkerEventKind.Done:
                        LogFinished(lessonId, timeProvider.GetElapsedTime(started), runtime, workerEvent.Value, segments.Count);
                        return new WorkerOutcome(Language: workerEvent.Value, Segments: segments);
                }
            }
        }
        catch (WorkerCrashedException ex)
        {
            LogCrashed(lessonId, ex.ExitCode, runtime);
            var onGpu = !job.ForceCpu && runtime is not null && !runtime.StartsWith("Cpu", StringComparison.Ordinal);
            return new WorkerOutcome(ErrorCode: TranscriptionErrors.ProcessCrashed, CrashedOnGpu: onGpu);
        }

        return new WorkerOutcome(ErrorCode: TranscriptionErrors.Unexpected);
    }

    private async Task SaveOutcomeAsync(LessonTranscriptionRecord record, string hash, WhisperModel model, WorkerOutcome outcome, CancellationToken cancellationToken)
    {
        if (outcome.NoAudio)
        {
            await store.SaveRecordAsync(record with { Status = TranscriptionStatus.NoAudio }, cancellationToken).ConfigureAwait(false);
        }
        else if (outcome.Segments is { } segments)
        {
            var transcript = new Transcript(Guid.NewGuid(), TranscriptSource.Whisper, model.Name, outcome.Language, TranscriptSegmentFilter.Apply(segments));
            await store.SaveTranscriptAsync(hash, transcript, cancellationToken).ConfigureAwait(false);
            await store.SaveRecordAsync(record with { Status = TranscriptionStatus.Done, TranscriptId = transcript.Id }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await store.SaveRecordAsync(record with { Status = TranscriptionStatus.Failed, ErrorCode = outcome.ErrorCode }, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task FailAsync(Guid lessonId, string errorCode)
    {
        var record = await store.GetRecordAsync(lessonId, CancellationToken.None).ConfigureAwait(false);
        if (record is not null)
        {
            await store.SaveRecordAsync(record with { Status = TranscriptionStatus.Failed, ErrorCode = errorCode }, CancellationToken.None).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Transcrição concluída para {LessonId} em {Elapsed} (runtime {Runtime}, idioma {Language}, {SegmentCount} segmentos)")]
    private partial void LogFinished(Guid lessonId, TimeSpan elapsed, string? runtime, string? language, int segmentCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Processo de transcrição terminou com o código {ExitCode} na aula {LessonId} (runtime {Runtime})")]
    private partial void LogCrashed(Guid lessonId, int exitCode, string? runtime);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha com GPU na aula {LessonId}; o app passa a usar a CPU")]
    private partial void LogGpuFallback(Guid lessonId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "VLC sem speex_resampler na aula {LessonId}: a qualidade da transcrição pode cair")]
    private partial void LogLowQualityAudio(Guid lessonId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Legenda sem nenhum bloco válido na aula {LessonId}")]
    private partial void LogSubtitleInvalid(Guid lessonId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Legenda ilegível na aula {LessonId}")]
    private partial void LogSubtitleUnreadable(Guid lessonId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha inesperada na transcrição da aula {LessonId}")]
    private partial void LogUnexpected(Exception exception, Guid lessonId);

    private sealed record WorkerOutcome(
        bool NoAudio = false,
        string? ErrorCode = null,
        string? Language = null,
        IReadOnlyList<TranscriptSegment>? Segments = null,
        bool CrashedOnGpu = false);
}
