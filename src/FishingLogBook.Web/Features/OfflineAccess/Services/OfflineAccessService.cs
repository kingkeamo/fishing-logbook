using FishingLogBook.Web.Features.OfflineAccess.Clients;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.Users.Clients;

namespace FishingLogBook.Web.Features.OfflineAccess.Services;

public sealed class OfflineAccessService : IOfflineAccessService
{
    private readonly ICurrentUserClient _currentUserClient;
    private readonly IOfflineAccessPreferenceClient _preferenceClient;
    private readonly IOfflineAccessDeviceService _deviceService;

    public OfflineAccessService(ICurrentUserClient currentUserClient, IOfflineAccessPreferenceClient preferenceClient, IOfflineAccessDeviceService deviceService)
    {
        _currentUserClient = currentUserClient;
        _preferenceClient = preferenceClient;
        _deviceService = deviceService;
    }

    public async Task<OfflineAccessStatusModel> GetStatusAsync(CancellationToken cancellationToken)
    {
        var preference = await _preferenceClient.GetAsync(cancellationToken);
        var identity = await IdentityAsync(cancellationToken);
        var device = await _deviceService.GetStatusAsync(identity, cancellationToken);
        if (!preference.Enabled && device.State == "ready")
        {
            await _deviceService.RemoveAsync(identity, cancellationToken);
            return new OfflineAccessStatusModel(false, "not-configured");
        }

        return new OfflineAccessStatusModel(preference.Enabled, device.State);
    }

    public async Task<OfflineAccessStatusModel> SetupAsync(CancellationToken cancellationToken)
    {
        var identity = await IdentityAsync(cancellationToken);
        var device = await _deviceService.SetupAsync(identity, cancellationToken);
        if (device.State == "ready") await _preferenceClient.SetAsync(true, cancellationToken);
        return new OfflineAccessStatusModel(device.State == "ready", device.State, device);
    }

    public async Task<OfflineAccessStatusModel> RemoveFromDeviceAsync(CancellationToken cancellationToken)
    {
        var identity = await IdentityAsync(cancellationToken);
        await _deviceService.RemoveAsync(identity, cancellationToken);
        var preference = await _preferenceClient.GetAsync(cancellationToken);
        return new OfflineAccessStatusModel(preference.Enabled, "not-configured");
    }

    public async Task<OfflineAccessStatusModel> TurnOffAccountAsync(CancellationToken cancellationToken)
    {
        var identity = await IdentityAsync(cancellationToken);
        await _deviceService.RemoveAsync(identity, cancellationToken);
        await _preferenceClient.SetAsync(false, cancellationToken);
        return new OfflineAccessStatusModel(false, "not-configured");
    }

    private async Task<OfflineAccessIdentityModel> IdentityAsync(CancellationToken cancellationToken)
    {
        var current = await _currentUserClient.GetCurrentAsync(cancellationToken);
        return new OfflineAccessIdentityModel(current.UserId, current.Provider, current.Subject);
    }
}
