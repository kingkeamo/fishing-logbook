using System.Text.Json;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Stores;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Profile.Stores;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.OfflineAccess.Services;

public sealed class OfflinePreparationService(
    IAuthenticationService authentication,
    IAccountSessionCoordinator sessionCoordinator,
    IAnglerPreferencesService preferencesService,
    IAnglerPreferencesStore preferencesStore,
    IOfflineAccountStore accountStore,
    IOfflineCapabilityService capabilityService,
    ILogger<OfflinePreparationService> logger) : IOfflinePreparationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> EnsurePreparedAsync(ProfileDto profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        var session = authentication.CurrentSession;
        var lease = sessionCoordinator.Capture();
        if (session is null || lease is null ||
            lease.UserId != session.UserId ||
            profile.UserId != session.UserId ||
            !profile.OnboardingCompleted)
        {
            return false;
        }

        var capability = new OfflineCapabilityModel(session.Subject, session.UserId, IsDeviceReady: true);
        if (await IsPreparedAsync(capability, profile, cancellationToken))
        {
            return true;
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lease.CancellationToken);
            var refreshed = await preferencesService.RefreshAsync(linked.Token);
            if (!refreshed.IsSuccess || refreshed.Value is null || !IsEssential(refreshed.Value))
            {
                return false;
            }

            if (!sessionCoordinator.IsCurrent(lease) || authentication.CurrentSession?.UserId != lease.UserId)
            {
                return false;
            }

            var account = new OfflineAccountModel(session.Subject, session.UserId, session.Email, profile);
            await preferencesStore.SaveAsync(session.UserId, refreshed.Value, linked.Token);
            await accountStore.SaveAsync(account, linked.Token);
            var persistedPreferences = await preferencesStore.GetAsync(session.UserId, linked.Token);
            var persistedAccount = await accountStore.GetAsync(session.UserId, linked.Token);
            if (!Matches(account, persistedAccount) || !Matches(refreshed.Value, persistedPreferences))
            {
                return false;
            }

            if (!sessionCoordinator.IsCurrent(lease) || authentication.CurrentSession?.UserId != lease.UserId)
            {
                return false;
            }

            await capabilityService.MarkReadyAsync(session.Subject, session.UserId, linked.Token);
            if (!IsCurrent(lease))
            {
                await RevokeCommittedCapabilityAsync(session);
                return false;
            }

            if (!await IsPreparedAsync(capability, profile, linked.Token))
            {
                return false;
            }

            if (!IsCurrent(lease))
            {
                await RevokeCommittedCapabilityAsync(session);
                return false;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Offline account preparation could not be completed.");
            return false;
        }
    }

    public async Task<bool> IsPreparedAsync(
        OfflineCapabilityModel capability,
        CancellationToken cancellationToken)
    {
        return await IsPreparedAsync(capability, expectedProfile: null, cancellationToken);
    }

    private async Task<bool> IsPreparedAsync(
        OfflineCapabilityModel capability,
        ProfileDto? expectedProfile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capability);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var ready = await capabilityService.GetReadyAsync(cancellationToken);
            if (ready != capability)
            {
                return false;
            }

            var account = await accountStore.GetAsync(capability.UserId, cancellationToken);
            var preferences = await preferencesStore.GetAsync(capability.UserId, cancellationToken);
            return account is not null &&
                account.UserId == capability.UserId &&
                string.Equals(account.Subject, capability.Subject, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(account.Email) &&
                account.Profile.UserId == capability.UserId &&
                account.Profile.OnboardingCompleted &&
                (expectedProfile is null || account.Profile == expectedProfile) &&
                preferences is not null &&
                IsEssential(preferences);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Offline account readiness could not be verified.");
            return false;
        }
    }

    private static bool IsEssential(AnglerPreferencesModel preferences)
    {
        return preferences.Catalogue.Methods.Count > 0 && preferences.Catalogue.AllSpecies.Count > 0;
    }

    private bool IsCurrent(AccountSessionLeaseModel lease)
    {
        return sessionCoordinator.IsCurrent(lease) && authentication.CurrentSession?.UserId == lease.UserId;
    }

    private Task RevokeCommittedCapabilityAsync(AuthenticationSessionModel session)
    {
        return capabilityService.RevokeIfOwnedByAsync(
            session.Subject,
            session.UserId,
            CancellationToken.None);
    }

    private static bool Matches(OfflineAccountModel expected, OfflineAccountModel? actual)
    {
        return actual == expected;
    }

    private static bool Matches(AnglerPreferencesModel expected, AnglerPreferencesModel? actual)
    {
        return actual is not null &&
            JsonSerializer.Serialize(actual, SerializerOptions) ==
            JsonSerializer.Serialize(expected, SerializerOptions);
    }
}
