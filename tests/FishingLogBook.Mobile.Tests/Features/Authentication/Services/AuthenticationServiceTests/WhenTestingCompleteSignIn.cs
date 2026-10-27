using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Services.AuthenticationServiceTests;

public class WhenTestingCompleteSignIn : BaseAuthenticationServiceTest
{
    private static readonly Uri Callback =
        new("com.fishinglogbook.mobile.demo://oauth2redirect?code=code&state=state");

    [Fact]
    public async Task ItShouldRejectAndConsumeAnExpiredAuthorization()
    {
        // Arrange
        MockStore.ConsumePendingAuthorizationAsync().Returns(CreatePending(Now));
        // Act
        var result = await Sut.CompleteSignInAsync(Callback, CancellationToken.None);
        // Assert
        result.Should().Be(AuthenticationResultEnum.Invalid);
        Sut.CurrentSession.Should().BeNull();
        await MockOidcClient.DidNotReceive().CompleteLoginAsync(
            Arg.Any<Uri>(),
            Arg.Any<PendingAuthorizationModel>(),
            Arg.Any<CancellationToken>());
        await MockStore.Received(1).ConsumePendingAuthorizationAsync();
    }

    [Fact]
    public async Task ItShouldReportTheCompletionResultSoTheSignInPageStopsWaiting()
    {
        // Arrange
        var pending = CreatePending();
        var reported = new List<AuthenticationResultEnum>();
        Sut.SignInCompleted += (_, result) => reported.Add(result);
        MockStore.ConsumePendingAuthorizationAsync().Returns(pending);
        MockOidcClient.CompleteLoginAsync(Callback, pending, CancellationToken.None)
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.Transient));
        // Act
        var result = await Sut.CompleteSignInAsync(Callback, CancellationToken.None);
        // Assert
        result.Should().Be(AuthenticationResultEnum.TemporarilyUnavailable);
        reported.Should().Equal(AuthenticationResultEnum.TemporarilyUnavailable);
        Sut.HasPendingAuthorization.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRejectACallbackMismatchWithoutPersistingCredentials()
    {
        // Arrange
        var pending = CreatePending();
        MockStore.ConsumePendingAuthorizationAsync().Returns(pending, (PendingAuthorizationModel?)null);
        MockOidcClient.CompleteLoginAsync(Callback, pending, CancellationToken.None)
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.InvalidResponse));
        // Act
        var first = await Sut.CompleteSignInAsync(Callback, CancellationToken.None);
        var replay = await Sut.CompleteSignInAsync(Callback, CancellationToken.None);
        // Assert
        first.Should().Be(AuthenticationResultEnum.Invalid);
        replay.Should().Be(AuthenticationResultEnum.Invalid);
        await MockOidcClient.Received(1).CompleteLoginAsync(Callback, pending, CancellationToken.None);
        await MockStore.DidNotReceive().SaveCredentialAsync(Arg.Any<PersistedAuthenticationModel>());
    }

    [Fact]
    public async Task ItShouldNotExposeTheSessionWhenRotatedCredentialStorageFails()
    {
        // Arrange
        var pending = CreatePending();
        MockStore.ConsumePendingAuthorizationAsync().Returns(pending);
        MockOidcClient.CompleteLoginAsync(Callback, pending, CancellationToken.None)
            .Returns(CreateTokenResult());
        MockCurrentUserClient.GetCurrentAsync(AccessToken, CancellationToken.None)
            .Returns(new CurrentUserDto(UserId, Email, "Cognito", Subject));
        MockStore.SaveCredentialAsync(Arg.Any<PersistedAuthenticationModel>())
            .Returns(Task.FromException(new InvalidOperationException("Secure storage unavailable")));
        // Act
        var result = await Sut.CompleteSignInAsync(Callback, CancellationToken.None);
        // Assert
        result.Should().Be(AuthenticationResultEnum.StorageUnavailable);
        Sut.CurrentSession.Should().BeNull();
        await MockSessionCoordinator.Received(1).TransitionAsync(null, CancellationToken.None);
        await MockStore.DidNotReceive().RemoveCredentialAsync();
    }

    [Fact]
    public async Task ItShouldEstablishTheApiResolvedOwnerAfterValidOidcLogin()
    {
        // Arrange
        var pending = CreatePending();
        MockStore.ConsumePendingAuthorizationAsync().Returns(pending);
        MockOidcClient.CompleteLoginAsync(Callback, pending, CancellationToken.None)
            .Returns(CreateTokenResult());
        MockCurrentUserClient.GetCurrentAsync(AccessToken, CancellationToken.None)
            .Returns(new CurrentUserDto(UserId, Email, "Cognito", Subject));
        // Act
        var result = await Sut.CompleteSignInAsync(Callback, CancellationToken.None);
        // Assert
        result.Should().Be(AuthenticationResultEnum.Success);
        Sut.CurrentSession.Should().Be(new AuthenticationSessionModel(
            AccessToken,
            Now.AddHours(1),
            RefreshToken,
            Subject,
            Email,
            UserId));
        await MockStore.Received(1).SaveCredentialAsync(Arg.Is<PersistedAuthenticationModel>(
            value => value.RefreshToken == RefreshToken &&
                value.Subject == Subject &&
                value.UserId == UserId));
        await MockSessionCoordinator.Received(1).TransitionAsync(UserId, CancellationToken.None);
    }
}
