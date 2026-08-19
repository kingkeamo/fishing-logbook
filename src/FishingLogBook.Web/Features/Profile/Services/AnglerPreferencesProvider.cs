using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Offline;

namespace FishingLogBook.Web.Features.Profile.Services;

public sealed class AnglerPreferencesProvider : IAnglerPreferencesProvider
{
    private readonly IProfileClient _profileClient;
    private readonly IFishingPreferenceClient _fishingPreferenceClient;
    private readonly IAnglerPreferencesCache _cache;
    private readonly ILocalCatchOwnerService _localCatchOwner;

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

        var fresh = await TryLoadFromApiAsync(cancellationToken);
        if (fresh is not null)
        {
            await TryCacheAsync(userId, fresh, cancellationToken);
            return fresh;
        }

        return await TryLoadFromCacheAsync(userId, cancellationToken) ?? AnglerPreferencesModel.Empty;
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
        try
        {
            var profile = await _profileClient.GetOwnAsync(cancellationToken);
            var catalogue = await _fishingPreferenceClient.GetCatalogueAsync(cancellationToken);
            var preferences = await _fishingPreferenceClient.GetPreferencesAsync(cancellationToken);
            return new AnglerPreferencesModel(
                catalogue,
                preferences,
                profile.PreferredWeightUnit,
                profile.PreferredLengthUnit);
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
}
