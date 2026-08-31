using Microsoft.JSInterop;

namespace FishingLogBook.Web.Tests.Browser.Network.TestSupport;

public sealed class FakeNetworkJsRuntime : IJSRuntime
{
    public bool BrowserOnline { get; set; } = true;

    public string? LastIdentifier { get; private set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        LastIdentifier = identifier;
        if (identifier == "fishingLogBookNetwork.isOnline")
        {
            return new ValueTask<TValue>((TValue)(object)BrowserOnline);
        }

        return default;
    }
}
