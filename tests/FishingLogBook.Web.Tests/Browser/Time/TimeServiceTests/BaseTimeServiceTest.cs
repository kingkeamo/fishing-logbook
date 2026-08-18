using Microsoft.JSInterop;

namespace FishingLogBook.Web.Tests.Browser.Time.TimeServiceTests;

public class BaseTimeServiceTest
{
    protected sealed class FakeTimeJsRuntime : IJSRuntime, IJSObjectReference
    {
        public List<string> ImportPaths { get; } = [];

        public List<string> Invocations { get; } = [];

        public string? LastUtcIso { get; private set; }

        public string? LastLocalValue { get; private set; }

        public string? ToDateTimeLocalResult { get; set; } = "2026-08-17T14:00";

        public string? FromDateTimeLocalResult { get; set; } = "2026-08-17T10:00:00.000Z";

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
                ImportPaths.Add(args?[0] as string ?? string.Empty);
                return ValueTask.FromResult((TValue)(object)this);
            }

            Invocations.Add(identifier);
            if (identifier == "toDateTimeLocalValue")
            {
                LastUtcIso = args?[0] as string;
                return ValueTask.FromResult((TValue)(object?)ToDateTimeLocalResult!);
            }

            if (identifier == "fromDateTimeLocalValue")
            {
                LastLocalValue = args?[0] as string;
                return ValueTask.FromResult((TValue)(object?)FromDateTimeLocalResult!);
            }

            throw new InvalidOperationException(identifier);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
