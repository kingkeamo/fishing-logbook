using FishingLogBook.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Application.Tests.DiagnosticLogServiceTests;

public sealed class RecordingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public List<IReadOnlyList<KeyValuePair<string, object?>>> Scopes { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            Scopes.Add(pairs.ToArray());
        }

        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

public sealed class MemoryDeduplicator : IDiagnosticEventDeduplicator
{
    private readonly HashSet<Guid> _seen = [];

    public bool TryAccept(Guid id) => _seen.Add(id);
}
