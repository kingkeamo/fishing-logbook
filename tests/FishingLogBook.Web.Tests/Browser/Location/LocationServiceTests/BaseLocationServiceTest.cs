using System.Text.Json;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Features.Diagnostics.Services;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Browser.Location.LocationServiceTests;

public class BaseLocationServiceTest
{
    protected readonly IDiagnosticLogger MockDiagnostics = Substitute.For<IDiagnosticLogger>();
    protected readonly ScriptedJsRuntime Js = new();
    protected readonly LocationService Sut;

    protected BaseLocationServiceTest()
    {
        Sut = new LocationService(Js, MockDiagnostics);
    }

    protected sealed class ScriptedJsRuntime : IJSRuntime, IJSObjectReference
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
        private readonly List<string> _invoked = [];

        public string? Permission { get; set; } = "prompt";

        public bool Dismissed { get; set; }

        public string CurrentJson { get; set; } = "null";

        public Exception? CurrentFailure { get; set; }

        public IReadOnlyList<string> Invoked => _invoked;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            if (identifier == "import")
            {
                return new ValueTask<TValue>((TValue)(object)this);
            }

            _invoked.Add(identifier);
            switch (identifier)
            {
                case "queryPermission":
                    return new ValueTask<TValue>((TValue)(object)Permission!);
                case "isPromptDismissed":
                    return new ValueTask<TValue>((TValue)(object)Dismissed);
                case "getCurrent":
                    if (CurrentFailure is not null)
                    {
                        throw CurrentFailure;
                    }

                    return new ValueTask<TValue>(JsonSerializer.Deserialize<TValue>(CurrentJson, Options)!);
                case "setPromptDismissed":
                    return new ValueTask<TValue>(default(TValue)!);
                default:
                    throw new InvalidOperationException(identifier);
            }
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
