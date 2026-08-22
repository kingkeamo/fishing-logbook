using FishingLogBook.Web.Features.Diagnostics.Services;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Browser.Update;

public sealed class AppUpdateService : IAppUpdateService, IDisposable, IAsyncDisposable
{
    private const string ModulePath = "./js/browser/app-update.js";
    private const string FailureSource = "app update";

    private readonly IJSRuntime _jsRuntime;
    private readonly ILoggingService _logging;

    private DotNetObjectReference<AppUpdateService>? _reference;
    private IJSObjectReference? _module;
    private long _subscriptionToken;
    private bool _isUpdateReady;
    private bool _hasFailed;
    private bool _isActivating;
    private bool _isStarted;

    public AppUpdateService(IJSRuntime jsRuntime, ILoggingService logging)
    {
        _jsRuntime = jsRuntime;
        _logging = logging;
    }

    public event Action? StatusChanged;

    public AppUpdateStatus Status
    {
        get
        {
            if (_isActivating)
            {
                return AppUpdateStatus.Activating;
            }

            if (!_isUpdateReady)
            {
                return AppUpdateStatus.Current;
            }

            return _hasFailed ? AppUpdateStatus.Failed : AppUpdateStatus.Available;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;
        try
        {
            var module = await ImportModuleAsync(cancellationToken);
            _reference = DotNetObjectReference.Create(this);
            _subscriptionToken = await module.InvokeAsync<long>(
                "subscribeUpdateState",
                cancellationToken,
                _reference);
            Apply(await module.InvokeAsync<AppUpdateState>("getUpdateState", cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            await LogFailureAsync(exception);
        }
    }

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        if (!_isUpdateReady || _isActivating)
        {
            return;
        }

        _isActivating = true;
        _hasFailed = false;
        StatusChanged?.Invoke();

        try
        {
            var module = await ImportModuleAsync(cancellationToken);
            var requested = await module.InvokeAsync<bool>("applyUpdate", cancellationToken);
            if (requested)
            {
                return;
            }

            await FailAsync(null);
        }
        catch (OperationCanceledException)
        {
            _isActivating = false;
        }
        catch (Exception exception)
        {
            await FailAsync(exception);
        }
    }

    [JSInvokable]
    public void OnUpdateStateChanged(AppUpdateState state)
    {
        Apply(state);
    }

    public void Dispose()
    {
        _reference?.Dispose();
        _reference = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null && _reference is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("unsubscribeUpdateState", _subscriptionToken);
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
        }

        Dispose();
    }

    private void Apply(AppUpdateState state)
    {
        if (_isUpdateReady == state.IsUpdateReady)
        {
            return;
        }

        _isUpdateReady = state.IsUpdateReady;
        StatusChanged?.Invoke();
    }

    private async Task FailAsync(Exception? exception)
    {
        _isActivating = false;
        _hasFailed = true;
        StatusChanged?.Invoke();
        if (exception is not null)
        {
            await LogFailureAsync(exception);
        }
    }

    private async ValueTask<IJSObjectReference> ImportModuleAsync(CancellationToken cancellationToken)
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            cancellationToken,
            ModulePath);
        return _module;
    }

    private Task LogFailureAsync(Exception exception)
    {
        return _logging.LogErrorAsync(FailureSource, exception, CancellationToken.None);
    }
}
