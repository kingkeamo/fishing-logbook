using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Features.Profile.Models;

namespace FishingLogBook.Web.Features.Profile.Providers;

public sealed class ProfileSummaryProvider : IProfileSummaryProvider
{
    private readonly IProfileClient _profileClient;
    private readonly ILocalCatchOwnerService _localCatchOwner;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private Guid _rememberedUserId;
    private ProfileSummaryModel? _remembered;

    public event Action? Changed;

    public ProfileSummaryProvider(IProfileClient profileClient, ILocalCatchOwnerService localCatchOwner)
    {
        _profileClient = profileClient;
        _localCatchOwner = localCatchOwner;
    }

    public async Task<ProfileSummaryModel> GetAsync(CancellationToken cancellationToken)
    {
        var userId = await ResolveOwnerAsync(cancellationToken);
        if (userId == Guid.Empty)
        {
            return ProfileSummaryModel.Empty;
        }

        if (TryGetRemembered(userId) is { } alreadyLoaded)
        {
            return alreadyLoaded;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (TryGetRemembered(userId) is { } loadedWhileWaiting)
            {
                return loadedWhileWaiting;
            }

            var profile = await TryLoadAsync(cancellationToken);
            if (profile is null)
            {
                return ProfileSummaryModel.Empty;
            }

            var currentUserId = await ResolveOwnerAsync(cancellationToken);
            if (currentUserId != userId)
            {
                return ProfileSummaryModel.Empty;
            }

            return Remember(
                userId,
                new ProfileSummaryModel(userId, profile.DisplayName, profile.PhotographUrl));
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        Invalidate();
        await GetAsync(cancellationToken);
        Changed?.Invoke();
    }

    public void Invalidate()
    {
        _remembered = null;
        _rememberedUserId = Guid.Empty;
    }

    private ProfileSummaryModel? TryGetRemembered(Guid userId)
    {
        return _remembered is not null && _rememberedUserId == userId
            ? _remembered
            : null;
    }

    private ProfileSummaryModel Remember(Guid userId, ProfileSummaryModel summary)
    {
        _rememberedUserId = userId;
        _remembered = summary;
        return summary;
    }

    private async Task<ProfileDto?> TryLoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _profileClient.GetOwnAsync(cancellationToken);
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
}
