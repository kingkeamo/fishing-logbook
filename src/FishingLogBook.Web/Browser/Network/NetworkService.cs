using FishingLogBook.Web.Features.SystemStatus.Clients;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Browser.Network;

public sealed class NetworkService : INetworkService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ISystemStatusClient _systemStatusClient;
    private readonly ILogger<NetworkService> _logger;
    private DotNetObjectReference<NetworkService>? _dotNetReference;
    private int _probeGeneration;
    private bool _monitoring;

    public event Action<bool>? ConnectivityChanged;

    public NetworkService(
        IJSRuntime jsRuntime,
        ISystemStatusClient systemStatusClient,
        ILogger<NetworkService> logger)
    {
        _jsRuntime = jsRuntime;
        _systemStatusClient = systemStatusClient;
        _logger = logger;
    }

    public async Task<bool> IsOnlineAsync(CancellationToken cancellationToken)
    {
        if (!await IsBrowserOnlineAsync(cancellationToken))
        {
            return false;
        }

        return await _systemStatusClient.IsApiReachableAsync(cancellationToken);
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
        if (!isOnline)
        {
            Interlocked.Increment(ref _probeGeneration);
            ConnectivityChanged?.Invoke(false);
            return;
        }

        _ = PublishReachabilityAsync();
    }

    [JSInvokable]
    public void OnBrowserUsable()
    {
        _ = PublishReachabilityAsync();
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

    private async Task PublishReachabilityAsync()
    {
        var generation = Interlocked.Increment(ref _probeGeneration);
        bool reachable;
        try
        {
            reachable = await IsOnlineAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Could not verify API reachability after a browser event.");
            reachable = false;
        }

        if (generation == Volatile.Read(ref _probeGeneration))
        {
            ConnectivityChanged?.Invoke(reachable);
        }
    }

    private async Task<bool> IsBrowserOnlineAsync(CancellationToken cancellationToken)
    {
        return await _jsRuntime.InvokeAsync<bool>("fishingLogBookNetwork.isOnline", cancellationToken);
    }
}
