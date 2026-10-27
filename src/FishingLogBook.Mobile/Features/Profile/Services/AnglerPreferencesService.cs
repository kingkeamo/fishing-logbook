using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Stores;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Profile.Services;

public sealed class AnglerPreferencesService : IAnglerPreferencesService
{
    private static readonly ApiOutcomeEnum[] FailurePriority =
    [
        ApiOutcomeEnum.SessionExpired,
        ApiOutcomeEnum.Unavailable,
        ApiOutcomeEnum.Forbidden,
        ApiOutcomeEnum.ServerError,
        ApiOutcomeEnum.ValidationFailed,
        ApiOutcomeEnum.InvalidResponse
    ];

    private readonly object _memoryLock = new();
    private readonly IFishingPreferenceClient _preferenceClient;
    private readonly IFishingLocationClient _locationClient;
    private readonly IAnglerPreferencesStore _store;
    private readonly IAccountSessionCoordinator _sessionCoordinator;
    private readonly ILogger<AnglerPreferencesService> _logger;
    private Guid _rememberedUserId;
    private AnglerPreferencesModel? _remembered;

    public AnglerPreferencesService(
        IFishingPreferenceClient preferenceClient,
        IFishingLocationClient locationClient,
        IAnglerPreferencesStore store,
        IAccountSessionCoordinator sessionCoordinator,
        ILogger<AnglerPreferencesService> logger)
    {
        _preferenceClient = preferenceClient;
        _locationClient = locationClient;
        _store = store;
        _sessionCoordinator = sessionCoordinator;
        _logger = logger;
    }

    public async Task<AnglerPreferencesModel?> GetCachedAsync(CancellationToken cancellationToken)
    {
        var lease = _sessionCoordinator.Capture();
        if (lease is null)
        {
            return null;
        }

        var remembered = TryGetRemembered(lease.UserId);
        if (remembered is not null)
        {
            return remembered;
        }

        var stored = await TryReadStoredAsync(lease.UserId, cancellationToken);
        if (stored is null || !_sessionCoordinator.IsCurrent(lease))
        {
            return null;
        }

        return RememberIfAbsent(lease.UserId, stored);
    }

    public async Task<ApiResultModel<AnglerPreferencesModel>> RefreshAsync(CancellationToken cancellationToken)
    {
        var lease = _sessionCoordinator.Capture();
        if (lease is null)
        {
            return ApiResultModel<AnglerPreferencesModel>.Failed(ApiOutcomeEnum.SessionExpired);
        }

        var catalogueTask = _preferenceClient.GetCatalogueAsync(cancellationToken);
        var preferencesTask = _preferenceClient.GetPreferencesAsync(cancellationToken);
        var locationsTask = _locationClient.GetAsync(cancellationToken);
        await Task.WhenAll(catalogueTask, preferencesTask, locationsTask);
        var catalogue = await catalogueTask;
        var preferences = await preferencesTask;
        var locations = await locationsTask;
        var failure = SelectFailure(catalogue.Outcome, preferences.Outcome, locations.Outcome);
        if (failure is not null)
        {
            return ApiResultModel<AnglerPreferencesModel>.Failed(failure.Value);
        }

        var refreshed = new AnglerPreferencesModel(catalogue.Value!, preferences.Value!, locations.Value!);
        var committed = await _sessionCoordinator.TryCommitAsync(
            lease,
            token => CommitAsync(lease.UserId, refreshed, token),
            cancellationToken);
        if (!committed)
        {
            throw new OperationCanceledException("The account session changed while preferences were refreshing.");
        }

        return ApiResultModel<AnglerPreferencesModel>.Succeeded(refreshed);
    }

    public async Task<SaveResultModel<AnglerPreferencesModel>> SaveAsync(
        AccountSessionLeaseModel lease,
        FishingCatalogueDto catalogue,
        UpdateFishingPreferencesDto preferences,
        UpdateFishingLocationPreferencesDto locations,
        CancellationToken cancellationToken)
    {
        if (!_sessionCoordinator.IsCurrent(lease))
        {
            return SaveResultModel<AnglerPreferencesModel>.NotAttempted(ApiOutcomeEnum.SessionExpired);
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lease.CancellationToken);
        var savedPreferences = await _preferenceClient.UpdatePreferencesAsync(preferences, linkedCancellation.Token);
        if (!savedPreferences.IsSuccess)
        {
            return SaveResultModel<AnglerPreferencesModel>.WriteFailed(savedPreferences.Outcome, earlierWritesSaved: false);
        }

        if (!_sessionCoordinator.IsCurrent(lease))
        {
            throw new OperationCanceledException("The account session changed while preferences were saving.");
        }

        var savedLocations = await _locationClient.UpdateAsync(locations, linkedCancellation.Token);
        if (!savedLocations.IsSuccess)
        {
            return SaveResultModel<AnglerPreferencesModel>.WriteFailed(savedLocations.Outcome, earlierWritesSaved: true);
        }

        var saved = new AnglerPreferencesModel(catalogue, savedPreferences.Value!, savedLocations.Value!);
        var committed = await _sessionCoordinator.TryCommitAsync(
            lease,
            token => CommitAsync(lease.UserId, saved, token),
            linkedCancellation.Token);
        if (!committed)
        {
            throw new OperationCanceledException("The account session changed while preferences were saving.");
        }

        return SaveResultModel<AnglerPreferencesModel>.Saved(saved);
    }

    private static ApiOutcomeEnum? SelectFailure(params ApiOutcomeEnum[] outcomes)
    {
        foreach (var failure in FailurePriority)
        {
            if (outcomes.Contains(failure))
            {
                return failure;
            }
        }

        return null;
    }

    private async Task CommitAsync(Guid userId, AnglerPreferencesModel preferences, CancellationToken cancellationToken)
    {
        Remember(userId, preferences);
        try
        {
            await _store.SaveAsync(userId, preferences, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("Angler preferences could not be cached with {ExceptionType}.", exception.GetType().FullName);
        }
    }

    private async Task<AnglerPreferencesModel?> TryReadStoredAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            return await _store.GetAsync(userId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("Cached angler preferences could not be read with {ExceptionType}.", exception.GetType().FullName);
            return null;
        }
    }

    private AnglerPreferencesModel? TryGetRemembered(Guid userId)
    {
        lock (_memoryLock)
        {
            return _rememberedUserId == userId ? _remembered : null;
        }
    }

    private AnglerPreferencesModel RememberIfAbsent(Guid userId, AnglerPreferencesModel preferences)
    {
        lock (_memoryLock)
        {
            if (_rememberedUserId == userId && _remembered is not null)
            {
                return _remembered;
            }

            _rememberedUserId = userId;
            _remembered = preferences;
            return preferences;
        }
    }

    private void Remember(Guid userId, AnglerPreferencesModel preferences)
    {
        lock (_memoryLock)
        {
            _rememberedUserId = userId;
            _remembered = preferences;
        }
    }
}
