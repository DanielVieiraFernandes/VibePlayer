using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibePlayer.Core.Transcription;

/// <summary>
/// Conversa entre o app e o processo de transcrição: um job em JSON na entrada padrão e um evento
/// em JSON por linha na saída padrão. Enquanto a entrada ficar aberta, o processo sabe que o app
/// está vivo; quando ela fecha, ele sai, sem deixar processo órfão.
/// </summary>
public static class WorkerProtocol
{
    /// <summary>Argumento que faz o executável do app rodar como processo de transcrição.</summary>
    public const string Argument = "--transcription-worker";

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(TranscriptionJob job) => JsonSerializer.Serialize(job, Options);

    public static string Serialize(WorkerEvent workerEvent) => JsonSerializer.Serialize(workerEvent, Options);

    public static TranscriptionJob? ParseJob(string line) => JsonSerializer.Deserialize<TranscriptionJob>(line, Options);

    /// <summary>
    /// Evento da linha, ou <c>null</c> se ela não for um evento: bibliotecas nativas podem escrever
    /// na mesma saída, e uma linha estranha não pode encerrar a transcrição.
    /// </summary>
    public static WorkerEvent? TryParseEvent(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<WorkerEvent>(line, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
