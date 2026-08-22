using Microsoft.JSInterop;

namespace FishingLogBook.Web.Browser.Install;

public sealed class InstallService : IInstallService
{
    private const string ModulePath = "./js/browser/install.js";
    private readonly IJSRuntime _jsRuntime;

    public InstallService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<InstallState> GetStateAsync(CancellationToken cancellationToken)
    {
        var module = await ImportModuleAsync(cancellationToken);
        return await module.InvokeAsync<InstallState>("getInstallState", cancellationToken);
    }

    public async Task<InstallResult> PromptAsync(CancellationToken cancellationToken)
    {
        var module = await ImportModuleAsync(cancellationToken);
        var result = await module.InvokeAsync<string>("promptInstall", cancellationToken);
        return result switch
        {
            "accepted" => InstallResult.Accepted,
            "dismissed" => InstallResult.Dismissed,
            _ => InstallResult.Unavailable
        };
    }

    public async Task<IAsyncDisposable> SubscribeAsync(
        Func<InstallState, Task> onStateChanged,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onStateChanged);
        var module = await ImportModuleAsync(cancellationToken);
        var subscription = new InstallStateSubscription(module, onStateChanged);
        await subscription.StartAsync(cancellationToken);
        return subscription;
    }

    private ValueTask<IJSObjectReference> ImportModuleAsync(CancellationToken cancellationToken)
    {
        return _jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
    }
}
