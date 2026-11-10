using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.LocalCatchOwnerServiceTests;

public class WhenTestingSubjectClaims : BaseLocalCatchOwnerServiceTest
{
    private static AuthenticationStateProvider SignedInWith(params Claim[] claims)
    {
        var provider = Substitute.For<AuthenticationStateProvider>();
        provider.GetAuthenticationStateAsync().Returns(
            new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"))));
        return provider;
    }

    [Fact]
    public async Task ItShouldFallBackToTheNameIdentifierClaimWhenTheSubjectClaimIsBlank()
    {
        // Arrange
        var jsRuntime = new MemoryJsRuntime();
        var currentUser = CurrentUser(OwnerUserId);
        var sut = CreateSut(
            SignedInWith(new Claim("sub", "  "), new Claim(ClaimTypes.NameIdentifier, $"  {OtherSubject}  ")),
            currentUser,
            jsRuntime);

        // Act
        var userId = await sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        userId.Should().Be(OwnerUserId);
        jsRuntime.Items.Should().ContainSingle().Which.Key.Should().Contain(OtherSubject);
        await currentUser.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldResolveTheUserWithoutCachingWhenNeitherSubjectClaimIsPresent()
    {
        // Arrange
        var jsRuntime = new MemoryJsRuntime();
        var currentUser = CurrentUser(OwnerUserId);
        var sut = CreateSut(SignedInWith(new Claim(ClaimTypes.NameIdentifier, " ")), currentUser, jsRuntime);

        // Act
        var userId = await sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        userId.Should().Be(OwnerUserId);
        jsRuntime.Items.Should().BeEmpty();
        jsRuntime.SetItemCalls.Should().Be(0);
        await currentUser.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreferTheShortSubjectClaimOverTheNameIdentifier()
    {
        // Arrange
        var jsRuntime = new MemoryJsRuntime();
        var sut = CreateSut(
            SignedInWith(new Claim("sub", OwnerSubject), new Claim(ClaimTypes.NameIdentifier, OtherSubject)),
            CurrentUser(OwnerUserId),
            jsRuntime);

        // Act
        await sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        jsRuntime.Items.Should().ContainSingle().Which.Key.Should().Contain(OwnerSubject);
        jsRuntime.Items.Keys.Should().NotContain(key => key.Contains(OtherSubject));
    }
}
