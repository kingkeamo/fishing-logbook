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
        var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
        return await module.InvokeAsync<InstallState>("getInstallState", cancellationToken);
    }

    public async Task<InstallResult> PromptAsync(CancellationToken cancellationToken)
    {
        var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
        var result = await module.InvokeAsync<string>("promptInstall", cancellationToken);
        return result switch
        {
            "accepted" => InstallResult.Accepted,
            "dismissed" => InstallResult.Dismissed,
            _ => InstallResult.Unavailable
        };
    }
}
