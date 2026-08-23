using FishingLogBook.Web.Features.Diagnostics.Models;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Features.Diagnostics.Services;

public sealed class WebAuthnCapabilityProbeService : IWebAuthnCapabilityProbeService
{
    private const string ModulePath = "./js/browser/webauthn-capability-probe.js";
    private readonly IJSRuntime _jsRuntime;

    public WebAuthnCapabilityProbeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> HasMetadataAsync(CancellationToken cancellationToken)
    {
        var module = await ImportModuleAsync(cancellationToken);
        return await module.InvokeAsync<bool>("hasProbeMetadata", cancellationToken);
    }

    public async Task<WebAuthnCapabilityProbeResultModel> GetStatusAsync(CancellationToken cancellationToken)
    {
        var module = await ImportModuleAsync(cancellationToken);
        return await module.InvokeAsync<WebAuthnCapabilityProbeResultModel>("getProbeStatus", cancellationToken);
    }

    public async Task<WebAuthnCapabilityProbeResultModel> ProvisionAsync(CancellationToken cancellationToken)
    {
        var module = await ImportModuleAsync(cancellationToken);
        return await module.InvokeAsync<WebAuthnCapabilityProbeResultModel>("provisionTestCredential", cancellationToken);
    }

    public async Task<WebAuthnCapabilityProbeResultModel> TestOfflineUnlockAsync(
        CancellationToken cancellationToken)
    {
        var module = await ImportModuleAsync(cancellationToken);
        return await module.InvokeAsync<WebAuthnCapabilityProbeResultModel>("testOfflineUnlock", cancellationToken);
    }

    public async Task<WebAuthnCapabilityProbeResultModel> VerifyOnlineAsync(CancellationToken cancellationToken)
    {
        var module = await ImportModuleAsync(cancellationToken);
        return await module.InvokeAsync<WebAuthnCapabilityProbeResultModel>(
            "verifyOnlineCredential",
            cancellationToken);
    }

    public async Task RemoveMetadataAsync(CancellationToken cancellationToken)
    {
        var module = await ImportModuleAsync(cancellationToken);
        await module.InvokeVoidAsync("removeProbeMetadata", cancellationToken);
    }

    private ValueTask<IJSObjectReference> ImportModuleAsync(CancellationToken cancellationToken)
    {
        return _jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
    }
}
