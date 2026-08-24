using System.Text.Json;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Services.OfflineAccessDeviceServiceTests;

public class BaseOfflineAccessDeviceServiceTest
{
    protected sealed class FakeOfflineAccessJsRuntime : IJSRuntime, IJSObjectReference
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

        public OfflineAccessAvailabilityModel Availability { get; set; } = new("not-configured", "no-records");
        public OfflineAccessUnlockResultModel UnlockResult { get; set; } = new("not-configured", null, null);
        public List<string> Invocations { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Invocations.Add(identifier);
            if (identifier == "import")
            {
                return ValueTask.FromResult((TValue)(object)this);
            }

            object result = identifier switch
            {
                "hasReadyEntitlement" => Availability,
                "unlockDevice" => UnlockResult,
                _ => throw new InvalidOperationException(identifier)
            };
            var json = JsonSerializer.Serialize(result, Options);
            return ValueTask.FromResult(JsonSerializer.Deserialize<TValue>(json, Options)!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    protected static OfflineAccessDeviceService CreateSut(FakeOfflineAccessJsRuntime js) => new(js);
}
