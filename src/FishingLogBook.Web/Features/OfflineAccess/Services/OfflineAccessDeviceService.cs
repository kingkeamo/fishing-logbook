using FishingLogBook.Web.Features.OfflineAccess.Models;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Features.OfflineAccess.Services;

public sealed class OfflineAccessDeviceService : IOfflineAccessDeviceService
{
    private const string ModulePath = "./js/browser/offline-access.js";
    private readonly IJSRuntime _jsRuntime;

    public OfflineAccessDeviceService(IJSRuntime jsRuntime) => _jsRuntime = jsRuntime;

    public async Task<OfflineAccessDeviceResultModel> GetStatusAsync(OfflineAccessIdentityModel identity, CancellationToken cancellationToken)
    {
        var module = await ImportAsync(cancellationToken);
        return await module.InvokeAsync<OfflineAccessDeviceResultModel>("getDeviceStatus", cancellationToken, identity);
    }

    public async Task<OfflineAccessDeviceResultModel> SetupAsync(OfflineAccessIdentityModel identity, CancellationToken cancellationToken)
    {
        var module = await ImportAsync(cancellationToken);
        return await module.InvokeAsync<OfflineAccessDeviceResultModel>("setupDevice", cancellationToken, identity);
    }

    public async Task RemoveAsync(OfflineAccessIdentityModel identity, CancellationToken cancellationToken)
    {
        var module = await ImportAsync(cancellationToken);
        await module.InvokeVoidAsync("removeDevice", cancellationToken, identity);
    }

    private ValueTask<IJSObjectReference> ImportAsync(CancellationToken cancellationToken) =>
        _jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
}
