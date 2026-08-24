using Microsoft.JSInterop;

namespace FishingLogBook.Web.Browser.Network;

public sealed class NetworkService : INetworkService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<NetworkService> _logger;
    private DotNetObjectReference<NetworkService>? _dotNetReference;
    private bool _monitoring;

    public event Action<bool>? ConnectivityChanged;

    public NetworkService(IJSRuntime jsRuntime, ILogger<NetworkService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task<bool> IsOnlineAsync(CancellationToken cancellationToken)
    {
        return await _jsRuntime.InvokeAsync<bool>("fishingLogBookNetwork.isOnline", cancellationToken);
    }

    public async Task StartMonitoringAsync(CancellationToken cancellationToken)
    {
        if (_monitoring)
        {
            return;
        }

        _dotNetReference = DotNetObjectReference.Create(this);
        try
        {
            await _jsRuntime.InvokeVoidAsync(
                "fishingLogBookNetwork.startMonitoring",
                cancellationToken,
                _dotNetReference);
            _monitoring = true;
        }
        catch
        {
            _dotNetReference.Dispose();
            _dotNetReference = null;
            throw;
        }
    }

    [JSInvokable]
    public void OnBrowserConnectivityChanged(bool isOnline)
    {
        ConnectivityChanged?.Invoke(isOnline);
    }

    public async ValueTask DisposeAsync()
    {
        if (_monitoring)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("fishingLogBookNetwork.stopMonitoring");
            }
            catch (JSDisconnectedException exception)
            {
                _logger.LogDebug(exception, "Browser disconnected while stopping network monitoring.");
            }
        }

        _dotNetReference?.Dispose();
    }
}
