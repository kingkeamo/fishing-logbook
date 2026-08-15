using Microsoft.JSInterop;

namespace FishingLogBook.Web.Services;

public sealed class BrowserNetworkStatus : INetworkStatus
{
    private readonly IJSRuntime _jsRuntime;

    public BrowserNetworkStatus(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> IsOnlineAsync(CancellationToken cancellationToken)
    {
        return await _jsRuntime.InvokeAsync<bool>("fishingLogBookNetwork.isOnline", cancellationToken);
    }
}
