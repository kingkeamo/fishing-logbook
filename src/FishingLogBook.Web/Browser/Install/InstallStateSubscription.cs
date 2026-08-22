using Microsoft.JSInterop;

namespace FishingLogBook.Web.Browser.Install;

public sealed class InstallStateSubscription : IAsyncDisposable
{
    private readonly IJSObjectReference _module;
    private readonly Func<InstallState, Task> _onStateChanged;
    private DotNetObjectReference<InstallStateSubscription>? _reference;
    private long _token;
    private bool _disposed;

    internal InstallStateSubscription(IJSObjectReference module, Func<InstallState, Task> onStateChanged)
    {
        _module = module;
        _onStateChanged = onStateChanged;
    }

    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        _reference = DotNetObjectReference.Create(this);
        _token = await _module.InvokeAsync<long>("subscribeInstallState", cancellationToken, _reference);
    }

    [JSInvokable]
    public Task OnInstallStateChanged(InstallState state)
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        return _onStateChanged(state);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            await _module.InvokeVoidAsync("unsubscribeInstallState", _token);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            _reference?.Dispose();
        }
    }
}
