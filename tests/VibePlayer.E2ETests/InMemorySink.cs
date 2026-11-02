using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace VibePlayer.E2ETests;

/// <summary>
/// Guarda em memória tudo o que o app registrou, para os testes verificarem o log sem ler arquivos.
/// </summary>
public sealed class InMemorySink : ILogEventSink
{
    // Concorrente porque eventos chegam de várias threads (UI, pool, finalizer do GC).
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyCollection<LogEvent> Events => _events.ToArray();

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
}
