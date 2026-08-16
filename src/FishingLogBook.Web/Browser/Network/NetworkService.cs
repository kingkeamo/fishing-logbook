using Microsoft.JSInterop;

namespace FishingLogBook.Web.Browser.Network;

public sealed class NetworkService : INetworkService
{
    private readonly IJSRuntime _jsRuntime;

    public NetworkService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> IsOnlineAsync(CancellationToken cancellationToken)
    {
        return await _jsRuntime.InvokeAsync<bool>("fishingLogBookNetwork.isOnline", cancellationToken);
    }
}
