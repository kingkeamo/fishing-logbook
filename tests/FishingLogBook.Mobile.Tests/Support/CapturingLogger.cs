using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Tests.Support;

public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<(string Message, Exception? Exception)> _entries = [];

    public IReadOnlyList<(string Message, Exception? Exception)> Entries
    {
        get
        {
            lock (_entries)
            {
                return [.. _entries];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (_entries)
        {
            _entries.Add((formatter(state, exception), exception));
        }
    }
}
