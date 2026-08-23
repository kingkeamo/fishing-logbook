using System.Security.Claims;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Users.Clients;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.LocalCatchOwnerServiceTests;

public class BaseLocalCatchOwnerServiceTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected const string OwnerSubject = "cognito-sub-owner";
    protected const string OtherSubject = "cognito-sub-other";

    protected static LocalCatchOwnerService CreateSut(
        AuthenticationStateProvider authentication,
        ICurrentUserClient currentUserClient,
        IJSRuntime jsRuntime)
    {
        return new LocalCatchOwnerService(authentication, currentUserClient, jsRuntime);
    }

    protected static ICurrentUserClient CurrentUser(Guid userId, string email = "owner@example.test")
    {
        var client = Substitute.For<ICurrentUserClient>();
        client.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(new CurrentUserDto(userId, email, "Cognito", OwnerSubject));
        return client;
    }

    protected static AuthenticationStateProvider Authenticated(string? subject, string email = "owner@example.test")
    {
        return new SubjectAuthenticationStateProvider(true, subject, email);
    }

    protected static AuthenticationStateProvider Unauthenticated()
    {
        return new SubjectAuthenticationStateProvider(false, null);
    }

    protected sealed class MemoryJsRuntime : IJSRuntime
    {
        public Dictionary<string, string> Items { get; } = new(StringComparer.Ordinal);
        public int GetItemCalls { get; private set; }
        public int SetItemCalls { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            if (identifier == "localStorage.getItem")
            {
                GetItemCalls += 1;
                var key = args![0] as string ?? string.Empty;
                Items.TryGetValue(key, out var value);
                return ValueTask.FromResult((TValue)(object?)value!);
            }

            if (identifier == "localStorage.setItem")
            {
                SetItemCalls += 1;
                Items[args![0] as string ?? string.Empty] = args[1] as string ?? string.Empty;
                return ValueTask.FromResult(default(TValue)!);
            }

            throw new InvalidOperationException(identifier);
        }
    }

    private sealed class SubjectAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _user;

        public SubjectAuthenticationStateProvider(bool authenticated, string? subject, string email = "owner@example.test")
        {
            if (!authenticated)
            {
                _user = new ClaimsPrincipal(new ClaimsIdentity());
                return;
            }

            var claims = new List<Claim>
            {
                new("email", email)
            };
            if (!string.IsNullOrWhiteSpace(subject))
            {
                claims.Add(new Claim("sub", subject));
            }

            _user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(new AuthenticationState(_user));
        }
    }
}
