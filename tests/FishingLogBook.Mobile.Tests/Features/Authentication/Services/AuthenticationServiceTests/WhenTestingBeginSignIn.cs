using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Services.AuthenticationServiceTests;

public class WhenTestingBeginSignIn : BaseAuthenticationServiceTest
{
    [Fact]
    public async Task ItShouldClearPendingStateWhenTheBrowserCannotOpen()
    {
        // Arrange
        var pending = CreatePending();
        MockOidcClient.PrepareLoginAsync(CancellationToken.None).Returns(pending);
        MockBrowser.OpenAsync(new Uri(pending.StartUrl), new Uri(pending.RedirectUri), CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Browser unavailable")));
        // Act
        var result = await Sut.BeginSignInAsync(CancellationToken.None);
        // Assert
        result.Should().Be(AuthenticationResultEnum.TemporarilyUnavailable);
        Sut.HasPendingAuthorization.Should().BeFalse();
        await MockStore.Received(2).RemovePendingAuthorizationAsync();
        await MockStore.Received(1).SavePendingAuthorizationAsync(Arg.Is<PendingAuthorizationModel>(
            value => value == pending));
    }

    [Theory]
    [InlineData(SignInProviderEnum.Google)]
    [InlineData(SignInProviderEnum.Apple)]
    public async Task ItShouldRequestTheSelectedProviderWhenStartingTheBrowserSignIn(SignInProviderEnum provider)
    {
        // Arrange
        var pending = CreatePending();
        MockOidcClient.PrepareLoginAsync(CancellationToken.None, provider).Returns(pending);
        // Act
        var result = await Sut.BeginSignInAsync(CancellationToken.None, provider);
        // Assert
        result.Should().Be(AuthenticationResultEnum.Pending);
        await MockOidcClient.Received(1).PrepareLoginAsync(CancellationToken.None, provider);
        await MockOidcClient.DidNotReceive().PrepareLoginAsync(CancellationToken.None, SignInProviderEnum.EmailPassword);
        await MockBrowser.Received(1).OpenAsync(
            new Uri(pending.StartUrl),
            new Uri(pending.RedirectUri),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldPersistTheShortLivedTransactionBeforeOpeningTheBrowser()
    {
        // Arrange
        var pending = CreatePending();
        MockOidcClient.PrepareLoginAsync(CancellationToken.None).Returns(pending);
        // Act
        var result = await Sut.BeginSignInAsync(CancellationToken.None);
        // Assert
        result.Should().Be(AuthenticationResultEnum.Pending);
        Sut.HasPendingAuthorization.Should().BeTrue();
        await MockStore.Received(1).RemovePendingAuthorizationAsync();
        await MockStore.Received(1).SavePendingAuthorizationAsync(Arg.Is<PendingAuthorizationModel>(
            value => value == pending && value.ExpiresAt == Now.AddMinutes(5)));
        await MockBrowser.Received(1).OpenAsync(
            new Uri(pending.StartUrl),
            new Uri(pending.RedirectUri),
            CancellationToken.None);
    }
}
