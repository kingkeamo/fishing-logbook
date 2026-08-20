using Microsoft.Extensions.Logging;

namespace FishingLogBook.Infrastructure.Tests.Common;

public sealed class RecordingLogger<T> : ILogger<T>
{
    public List<RecordedLog> Records { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
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
        Records.Add(new RecordedLog(logLevel, exception, formatter(state, exception)));
    }

    public sealed record RecordedLog(LogLevel Level, Exception? Exception, string Message);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
