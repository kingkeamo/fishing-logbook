using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Offline;

namespace FishingLogBook.Web.Features.Profile.Services;

public sealed class AnglerPreferencesProvider : IAnglerPreferencesProvider
{
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(10);

    private readonly IProfileClient _profileClient;
    private readonly IFishingPreferenceClient _fishingPreferenceClient;
    private readonly IAnglerPreferencesCache _cache;
    private readonly ILocalCatchOwnerService _localCatchOwner;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private Guid _rememberedUserId;
    private AnglerPreferencesModel? _remembered;

    public AnglerPreferencesProvider(
        IProfileClient profileClient,
        IFishingPreferenceClient fishingPreferenceClient,
        IAnglerPreferencesCache cache,
        ILocalCatchOwnerService localCatchOwner)
    {
        _profileClient = profileClient;
        _fishingPreferenceClient = fishingPreferenceClient;
        _cache = cache;
        _localCatchOwner = localCatchOwner;
    }

    public async Task<AnglerPreferencesModel> GetAsync(CancellationToken cancellationToken)
    {
        var userId = await ResolveOwnerAsync(cancellationToken);
        if (userId == Guid.Empty)
        {
            return AnglerPreferencesModel.Empty;
        }

        if (TryGetRemembered(userId) is { } alreadyLoaded)
        {
            return alreadyLoaded;
        }

        var loaded = await LoadAsync(userId, cancellationToken);
        if (loaded.CameFromCache)
        {
            _ = RefreshAsync(userId);
        }

        return loaded.Preferences;
    }

    public void Invalidate()
    {
        _remembered = null;
        _rememberedUserId = Guid.Empty;
    }

    private async Task<LoadedPreferences> LoadAsync(Guid userId, CancellationToken cancellationToken)
    {
        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (TryGetRemembered(userId) is { } loadedWhileWaiting)
            {
                return new LoadedPreferences(loadedWhileWaiting, false);
            }

            var cached = await TryLoadFromCacheAsync(userId, cancellationToken);
            if (cached is not null)
            {
                return new LoadedPreferences(Remember(userId, cached), true);
            }

            var fresh = await TryLoadFromApiAsync(cancellationToken);
            if (fresh is null)
            {
                return new LoadedPreferences(AnglerPreferencesModel.Empty, false);
            }

            await TryCacheAsync(userId, fresh, cancellationToken);
            return new LoadedPreferences(Remember(userId, fresh), false);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task RefreshAsync(Guid userId)
    {
        await _loadLock.WaitAsync(CancellationToken.None);
        try
        {
            var fresh = await TryLoadFromApiAsync(CancellationToken.None);
            if (fresh is null)
            {
                return;
            }

            await TryCacheAsync(userId, fresh, CancellationToken.None);
            if (_rememberedUserId == userId)
            {
                Remember(userId, fresh);
            }
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private AnglerPreferencesModel? TryGetRemembered(Guid userId)
    {
        return _remembered is not null && _rememberedUserId == userId
            ? _remembered
            : null;
    }

    private AnglerPreferencesModel Remember(Guid userId, AnglerPreferencesModel preferences)
    {
        _rememberedUserId = userId;
        _remembered = preferences;
        return preferences;
    }

    private async Task<Guid> ResolveOwnerAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _localCatchOwner.GetUserIdAsync(cancellationToken);
        }
        catch (Exception)
        {
            return Guid.Empty;
        }
    }

    private async Task<AnglerPreferencesModel?> TryLoadFromApiAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ApiTimeout);
        var profileTask = _profileClient.GetOwnAsync(timeout.Token);
        var catalogueTask = _fishingPreferenceClient.GetCatalogueAsync(timeout.Token);
        var preferencesTask = _fishingPreferenceClient.GetPreferencesAsync(timeout.Token);
        try
        {
            await Task.WhenAll(profileTask, catalogueTask, preferencesTask);
            var profile = await profileTask;
            return new AnglerPreferencesModel(
                await catalogueTask,
                await preferencesTask,
                profile.PreferredWeightUnit,
                profile.PreferredLengthUnit);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task TryCacheAsync(
        Guid userId,
        AnglerPreferencesModel preferences,
        CancellationToken cancellationToken)
    {
        try
        {
            await _cache.SaveAsync(userId, preferences, cancellationToken);
        }
        catch (Exception)
        {
        }
    }

    private async Task<AnglerPreferencesModel?> TryLoadFromCacheAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _cache.GetAsync(userId, cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private sealed record LoadedPreferences(AnglerPreferencesModel Preferences, bool CameFromCache);
}
