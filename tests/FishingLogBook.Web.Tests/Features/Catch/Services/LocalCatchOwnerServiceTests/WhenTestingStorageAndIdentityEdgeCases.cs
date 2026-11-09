using System.Security.Claims;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Users.Clients;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.LocalCatchOwnerServiceTests;

public class WhenTestingStorageAndIdentityEdgeCases : BaseLocalCatchOwnerServiceTest, IDisposable
{
    private const string GetItem = "localStorage.getItem";
    private const string SetItem = "localStorage.setItem";

    private readonly BunitContext _context = new();

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private static AuthenticationStateProvider WithClaims(params Claim[] claims)
    {
        var provider = Substitute.For<AuthenticationStateProvider>();
        provider.GetAuthenticationStateAsync().Returns(
            new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))));
        return provider;
    }

    [Fact]
    public async Task ItShouldFallBackToTheNameIdentifierWhenThereIsNoSubjectClaim()
    {
        // Arrange
        _context.JSInterop.Setup<string?>(GetItem, _ => true).SetResult(null);
        _context.JSInterop.SetupVoid(SetItem, _ => true).SetVoidResult();
        var sut = CreateSut(
            WithClaims(new Claim(ClaimTypes.NameIdentifier, "  name-id  ")),
            CurrentUser(OwnerUserId),
            _context.Services.GetRequiredService<IJSRuntime>());

        // Act
        var userId = await sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        userId.Should().Be(OwnerUserId);
        _context.JSInterop.Invocations[GetItem].Should().ContainSingle()
            .Which.Arguments[0].Should().Be("fishingLogBook.localUserId.name-id");
        _context.JSInterop.Invocations[SetItem].Should().ContainSingle()
            .Which.Arguments.Should().Equal("fishingLogBook.localUserId.name-id", OwnerUserId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldNeitherReadNorWriteTheCacheWhenThereIsNoSubjectAtAll()
    {
        // Arrange
        var sut = CreateSut(
            WithClaims(new Claim("email", "owner@example.test")),
            CurrentUser(OwnerUserId),
            _context.Services.GetRequiredService<IJSRuntime>());

        // Act
        var userId = await sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        userId.Should().Be(OwnerUserId);
        _context.JSInterop.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldTreatABlankSubjectAsNoSubject()
    {
        // Arrange
        var sut = CreateSut(
            WithClaims(new Claim("sub", "   "), new Claim(ClaimTypes.NameIdentifier, " ")),
            CurrentUser(OwnerUserId),
            _context.Services.GetRequiredService<IJSRuntime>());

        // Act
        var userId = await sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        userId.Should().Be(OwnerUserId);
        _context.JSInterop.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldAskTheApiWhenTheStorageCannotBeRead()
    {
        // Arrange
        _context.JSInterop.Setup<string?>(GetItem, _ => true).SetException(new JSException("blocked"));
        _context.JSInterop.SetupVoid(SetItem, _ => true).SetVoidResult();
        var client = CurrentUser(OwnerUserId);
        var sut = CreateSut(Authenticated(OwnerSubject), client, _context.Services.GetRequiredService<IJSRuntime>());

        // Act
        var userId = await sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        userId.Should().Be(OwnerUserId);
        await client.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("")]
    public async Task ItShouldIgnoreAnUnusableCachedValueAndAskTheApi(string cached)
    {
        // Arrange
        _context.JSInterop.Setup<string?>(GetItem, _ => true).SetResult(cached);
        _context.JSInterop.SetupVoid(SetItem, _ => true).SetVoidResult();
        var client = CurrentUser(OwnerUserId);
        var sut = CreateSut(Authenticated(OwnerSubject), client, _context.Services.GetRequiredService<IJSRuntime>());

        // Act
        var userId = await sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        userId.Should().Be(OwnerUserId);
        await client.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
        _context.JSInterop.Invocations[SetItem].Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldStillReturnTheUserWhenTheCacheCannotBeWritten()
    {
        // Arrange
        _context.JSInterop.Setup<string?>(GetItem, _ => true).SetResult(null);
        _context.JSInterop.SetupVoid(SetItem, _ => true).SetException(new JSException("quota"));
        var sut = CreateSut(
            Authenticated(OwnerSubject),
            CurrentUser(OwnerUserId),
            _context.Services.GetRequiredService<IJSRuntime>());

        // Act
        var userId = await sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        userId.Should().Be(OwnerUserId);
    }

    [Fact]
    public async Task ItShouldNotCacheAnUnresolvedUser()
    {
        // Arrange
        _context.JSInterop.Setup<string?>(GetItem, _ => true).SetResult(null);
        var sut = CreateSut(
            Authenticated(OwnerSubject),
            CurrentUser(Guid.Empty),
            _context.Services.GetRequiredService<IJSRuntime>());

        // Act
        var act = () => sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _context.JSInterop.Invocations.Should().NotContain(invocation => invocation.Identifier == SetItem);
    }
}
