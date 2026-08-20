using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Features.Onboarding.Services;

public sealed class OnboardingService : IOnboardingService
{
    private const string CacheKeyPrefix = "fishingLogBook.onboardingCompleted.";

    private readonly ILocalCatchOwnerService _ownerService;
    private readonly IProfileClient _profileClient;
    private readonly IJSRuntime _jsRuntime;

    public OnboardingService(
        ILocalCatchOwnerService ownerService,
        IProfileClient profileClient,
        IJSRuntime jsRuntime)
    {
        _ownerService = ownerService;
        _profileClient = profileClient;
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> IsCompletedAsync(CancellationToken cancellationToken)
    {
        var userId = await _ownerService.GetUserIdAsync(cancellationToken);
        var cached = await ReadAsync(userId, cancellationToken);
        if (cached == true)
        {
            return true;
        }

        try
        {
            var profile = await _profileClient.GetOwnAsync(cancellationToken);
            await WriteAsync(userId, profile.OnboardingCompleted, cancellationToken);
            return profile.OnboardingCompleted;
        }
        catch (Exception) when (cached is not null)
        {
            return cached.Value;
        }
        catch (HttpRequestException)
        {
            // A first offline launch must not trap an established user in onboarding.
            return true;
        }
    }

    public async Task CompleteAsync(CancellationToken cancellationToken)
    {
        var userId = await _ownerService.GetUserIdAsync(cancellationToken);
        var profile = await _profileClient.CompleteOnboardingAsync(cancellationToken);
        if (!profile.OnboardingCompleted)
        {
            throw new InvalidOperationException("The server did not record onboarding completion.");
        }

        await WriteAsync(userId, true, cancellationToken);
    }

    private async Task<bool?> ReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>(
                "localStorage.getItem",
                cancellationToken,
                CacheKeyPrefix + userId.ToString("D"));
            return bool.TryParse(value, out var completed) ? completed : null;
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException)
        {
            return null;
        }
    }

    private async Task WriteAsync(Guid userId, bool completed, CancellationToken cancellationToken)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync(
                "localStorage.setItem",
                cancellationToken,
                CacheKeyPrefix + userId.ToString("D"),
                completed.ToString());
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException)
        {
        }
    }
}
