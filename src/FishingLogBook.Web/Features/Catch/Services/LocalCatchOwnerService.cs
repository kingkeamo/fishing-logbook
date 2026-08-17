using System.Security.Claims;
using FishingLogBook.Web.Features.Users.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Features.Catch.Services;

public sealed class LocalCatchOwnerService : ILocalCatchOwnerService
{
    private const string CacheKeyPrefix = "fishingLogBook.localUserId.";

    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly ICurrentUserClient _currentUserClient;
    private readonly IJSRuntime _jsRuntime;

    public LocalCatchOwnerService(
        AuthenticationStateProvider authenticationStateProvider,
        ICurrentUserClient currentUserClient,
        IJSRuntime jsRuntime)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _currentUserClient = currentUserClient;
        _jsRuntime = jsRuntime;
    }

    public async Task<Guid> GetUserIdAsync(CancellationToken cancellationToken)
    {
        var subject = await GetSubjectAsync();
        var cached = await TryReadCachedUserIdAsync(subject, cancellationToken);
        if (cached is not null)
        {
            return cached.Value;
        }

        var current = await _currentUserClient.GetCurrentAsync(cancellationToken);
        if (current.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("The current user could not be resolved.");
        }

        await TryWriteCachedUserIdAsync(subject, current.UserId, cancellationToken);
        return current.UserId;
    }

    private async Task<string?> GetSubjectAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = state.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("The current user is not signed in.");
        }

        var subject = user.FindFirst("sub")?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(subject))
        {
            return subject;
        }

        subject = user.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        return subject;
    }

    private async Task<Guid?> TryReadCachedUserIdAsync(string? subject, CancellationToken cancellationToken)
    {
        if (subject is null)
        {
            return null;
        }

        try
        {
            var stored = await _jsRuntime.InvokeAsync<string?>(
                "localStorage.getItem",
                cancellationToken,
                CacheKeyPrefix + subject);
            if (Guid.TryParse(stored, out var userId) && userId != Guid.Empty)
            {
                return userId;
            }
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        return null;
    }

    private async Task TryWriteCachedUserIdAsync(
        string? subject,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (subject is null)
        {
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync(
                "localStorage.setItem",
                cancellationToken,
                CacheKeyPrefix + subject,
                userId.ToString("D"));
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
