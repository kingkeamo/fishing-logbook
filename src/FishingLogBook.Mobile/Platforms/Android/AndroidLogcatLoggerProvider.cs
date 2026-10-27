using Microsoft.Extensions.Logging;
using AndroidLog = Android.Util.Log;

namespace FishingLogBook.Mobile;

public sealed class AndroidLogcatLoggerProvider : ILoggerProvider
{
    public const string Tag = "FishingLogBook";

    public ILogger CreateLogger(string categoryName)
    {
        return new AndroidLogcatLogger(categoryName);
    }

    public void Dispose()
    {
    }

    private sealed class AndroidLogcatLogger(string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = $"{categoryName}: {formatter(state, exception)}";
            if (exception is not null)
            {
                message = $"{message}{System.Environment.NewLine}{exception}";
            }

            switch (logLevel)
            {
                case LogLevel.Critical:
                case LogLevel.Error:
                    AndroidLog.Error(Tag, message);
                    break;
                case LogLevel.Warning:
                    AndroidLog.Warn(Tag, message);
                    break;
                default:
                    AndroidLog.Info(Tag, message);
                    break;
            }
        }
    }
}
