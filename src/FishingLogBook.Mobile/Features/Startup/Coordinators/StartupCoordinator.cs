using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.OfflineAccess.Enums;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Startup.Enums;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Startup.Coordinators;

public sealed class StartupCoordinator(
    IConnectivityService connectivity,
    IAuthenticationService authentication,
    IOfflineCapabilityService offlineCapability,
    IOfflinePreparationService offlinePreparation,
    IDeviceUnlockService deviceUnlock,
    IOfflineSessionService offlineSession,
    IAccountSessionCoordinator accountSession,
    UiTextService text,
    TimeSpan restorationTimeout) : IStartupCoordinator
{
    public async Task<StartupResultEnum> StartAsync(CancellationToken cancellationToken)
    {
        if (!connectivity.IsOnline)
        {
            return await UnlockOfflineAsync(cancellationToken);
        }

        var authenticationResult = await RestoreOnlineAsync(cancellationToken);
        if (authenticationResult == AuthenticationResultEnum.Success)
        {
            return authentication.CurrentSession is null
                ? StartupResultEnum.SignInRequired
                : StartupResultEnum.OnlineSessionReady;
        }

        if (authenticationResult == AuthenticationResultEnum.Pending)
        {
            return StartupResultEnum.SignInRequired;
        }

        if (authenticationResult == AuthenticationResultEnum.Invalid)
        {
            return StartupResultEnum.SignInRequired;
        }

        return connectivity.IsOnline
            ? StartupResultEnum.ServiceUnavailable
            : await UnlockOfflineAsync(cancellationToken);
    }

    private async Task<AuthenticationResultEnum> RestoreOnlineAsync(CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(restorationTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await authentication.InitializeAsync(linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthenticationResultEnum.TemporarilyUnavailable;
        }
    }

    private async Task<StartupResultEnum> UnlockOfflineAsync(CancellationToken cancellationToken)
    {
        var capability = await offlineCapability.GetReadyAsync(cancellationToken);
        if (capability is null)
        {
            return StartupResultEnum.OfflineAccessUnavailable;
        }

        if (!await offlinePreparation.IsPreparedAsync(capability, cancellationToken))
        {
            return StartupResultEnum.OfflineAccessUnavailable;
        }

        var unlockResult = await deviceUnlock.UnlockAsync(text["OfflineUnlockReason"], cancellationToken);
        if (unlockResult != DeviceUnlockResultEnum.Success)
        {
            return unlockResult == DeviceUnlockResultEnum.Cancelled
                ? StartupResultEnum.UnlockCancelled
                : StartupResultEnum.UnlockFailed;
        }

        if (connectivity.IsOnline)
        {
            return await ResolveOnlineRestorationAsync(capability, cancellationToken);
        }

        return await ActivateOfflineAsync(capability, cancellationToken);
    }

    private async Task<StartupResultEnum> ResolveOnlineRestorationAsync(
        OfflineCapabilityModel verifiedCapability,
        CancellationToken cancellationToken)
    {
        var authenticationResult = await RestoreOnlineAsync(cancellationToken);
        if (authenticationResult == AuthenticationResultEnum.Success)
        {
            return authentication.CurrentSession is null
                ? StartupResultEnum.SignInRequired
                : StartupResultEnum.OnlineSessionReady;
        }

        if (authenticationResult is AuthenticationResultEnum.Pending or AuthenticationResultEnum.Invalid)
        {
            return StartupResultEnum.SignInRequired;
        }

        return connectivity.IsOnline
            ? StartupResultEnum.ServiceUnavailable
            : await ActivateOfflineAsync(verifiedCapability, cancellationToken);
    }

    private async Task<StartupResultEnum> ActivateOfflineAsync(
        OfflineCapabilityModel capability,
        CancellationToken cancellationToken)
    {
        await accountSession.TransitionAsync(capability.UserId, cancellationToken);
        offlineSession.Activate(capability);
        return StartupResultEnum.OfflineSessionReady;
    }
}
