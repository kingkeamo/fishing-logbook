using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Mobile.Features.Photographs.Providers;

public sealed class PhotographLoggerProvider(Action<string> write) : ILoggerProvider
{
    public static readonly EventId PipelineEvent = new(24701, "PhotographPipeline");

    public ILogger CreateLogger(string categoryName)
    {
        return categoryName is
            "FishingLogBook.Mobile.Features.Photographs.Services.PhotographPreparationService" or
            "FishingLogBook.Mobile.Features.Photographs.Storage.Stores.PhotographFileStore" or
            "FishingLogBook.Mobile.Features.Photographs.ViewModels.PhotographCollectionEditorViewModel" or
            "FishingLogBook.Mobile.Features.Trips.Services.TripPhotographService" or
            "FishingLogBook.Mobile.Native.Adapters.PhotographMediaAdapter"
            ? new PhotographLogger(write)
            : NullLogger.Instance;
    }

    public void Dispose()
    {
    }

    private sealed class PhotographLogger(Action<string> write) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel is >= LogLevel.Information and < LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || (eventId != PipelineEvent && exception is null && logLevel < LogLevel.Warning))
            {
                return;
            }

            try
            {
                var message = formatter(state, exception);
                write(exception is null
                    ? $"FishingLogBook photograph: {message}"
                    : $"FishingLogBook photograph: {message}{Environment.NewLine}{exception}");
            }
            catch
            {
            }
        }
    }
}
