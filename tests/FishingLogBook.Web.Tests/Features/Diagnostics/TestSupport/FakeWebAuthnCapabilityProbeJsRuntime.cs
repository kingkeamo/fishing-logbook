using System.Text.Json;
using FishingLogBook.Web.Features.Diagnostics.Models;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.TestSupport;

public sealed class FakeWebAuthnCapabilityProbeJsRuntime : IJSRuntime, IJSObjectReference
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public WebAuthnCapabilityProbeResultModel Status { get; set; } = new();

    public WebAuthnCapabilityProbeResultModel ProvisionResult { get; set; } = new();

    public WebAuthnCapabilityProbeResultModel OfflineResult { get; set; } = new();

    public WebAuthnCapabilityProbeResultModel OnlineVerificationResult { get; set; } = new();

    public bool HasMetadata { get; set; }

    public List<string> Invocations { get; } = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier,
        CancellationToken cancellationToken,
        object?[]? args)
    {
        Invocations.Add(identifier);
        if (identifier == "import")
        {
            return new ValueTask<TValue>((TValue)(object)this);
        }

        object? result = identifier switch
        {
            "getProbeStatus" => Status,
            "hasProbeMetadata" => HasMetadata,
            "provisionTestCredential" => ProvisionResult,
            "verifyOnlineCredential" => OnlineVerificationResult,
            "testOfflineUnlock" => OfflineResult,
            "removeProbeMetadata" => null,
            _ => throw new InvalidOperationException(identifier)
        };

        if (result is null)
        {
            return new ValueTask<TValue>(default(TValue)!);
        }

        var json = JsonSerializer.Serialize(result, Options);
        return new ValueTask<TValue>(JsonSerializer.Deserialize<TValue>(json, Options)!);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
