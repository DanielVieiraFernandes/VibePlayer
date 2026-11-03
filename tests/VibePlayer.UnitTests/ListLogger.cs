using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace VibePlayer.UnitTests;

/// <summary>Logger que guarda as mensagens para o teste conferir o que foi registrado.</summary>
public sealed class ListLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> _entries = new();

    public IReadOnlyCollection<(LogLevel Level, string Message, Exception? Exception)> Entries => _entries.ToArray();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _entries.Enqueue((logLevel, formatter(state, exception), exception));
}
