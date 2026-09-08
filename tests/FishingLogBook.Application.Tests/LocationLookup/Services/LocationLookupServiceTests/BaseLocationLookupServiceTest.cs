using FishingLogBook.Application.LocationLookup.Contracts.HttpClients;
using FishingLogBook.Application.LocationLookup.Services;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FishingLogBook.Application.Tests.LocationLookup.Services.LocationLookupServiceTests;

public class BaseLocationLookupServiceTest
{
    protected static (LocationLookupService Service, ILocationLookupClient Client, RecordingLogger Logger) CreateService()
    {
        var client = Substitute.For<ILocationLookupClient>();
        var logger = new RecordingLogger();
        return (new LocationLookupService(client, new LocationLookupCacheService(), logger), client, logger);
    }

    protected sealed class RecordingLogger : ILogger<LocationLookupService>
    {
        public List<string> Messages { get; } = [];

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
}
