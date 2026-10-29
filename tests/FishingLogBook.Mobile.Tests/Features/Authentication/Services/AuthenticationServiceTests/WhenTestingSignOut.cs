using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Services.AuthenticationServiceTests;

public class WhenTestingSignOut : BaseAuthenticationServiceTest
{
    [Fact]
    public async Task ItShouldCompleteLocalSignOutWhenRemoteCleanupFails()
    {
        // Arrange
        await EstablishSessionAsync();
        MockCognitoSessionClient.RevokeRefreshTokenAsync(RefreshToken, CancellationToken.None)
            .Returns(Task.FromException(new HttpRequestException("Revocation unavailable")));
        MockCognitoSessionClient.OpenLogoutAsync(CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Browser unavailable")));
        // Act
        var action = () => Sut.SignOutAsync(CancellationToken.None);
        await action.Should().NotThrowAsync();
        // Assert
        Sut.CurrentSession.Should().BeNull();
        await MockSessionCoordinator.Received(1).TransitionAsync(null, CancellationToken.None);
        await MockStore.Received(1).RemoveCredentialAsync();
        await MockOfflineCapability.Received(1).RevokeAsync(CancellationToken.None);
        await MockStore.Received(1).RemovePendingAuthorizationAsync();
        await MockCognitoSessionClient.Received(1).RevokeRefreshTokenAsync(
            RefreshToken,
            CancellationToken.None);
        await MockCognitoSessionClient.Received(1).OpenLogoutAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldNotRefreshAQueuedRequestAfterSignOutCompletes()
    {
        // Arrange
        await EstablishSessionAsync(Now.AddSeconds(30));
        var releaseRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MockStore.RemoveCredentialAsync().Returns(releaseRemoval.Task);
        // Act
        var signOut = Sut.SignOutAsync(CancellationToken.None);
        var queuedRequest = Sut.GetAccessTokenAsync(CancellationToken.None);
        releaseRemoval.SetResult();
        await signOut;
        var action = () => queuedRequest;
        // Assert
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        Sut.CurrentSession.Should().BeNull();
        await MockRefreshService.DidNotReceive().RefreshAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await MockSessionCoordinator.Received(1).TransitionAsync(UserId, CancellationToken.None);
        await MockSessionCoordinator.Received(1).TransitionAsync(null, CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldEndSignedOutWhenSignOutOverlapsAnInFlightRefresh()
    {
        // Arrange
        await EstablishSessionAsync(Now.AddSeconds(30));
        var refreshCompletion = new TaskCompletionSource<OidcTokenResultModel>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        MockRefreshService.RefreshAsync(RefreshToken, CancellationToken.None).Returns(refreshCompletion.Task);
        MockCurrentUserClient.GetCurrentAsync("refreshed-access", CancellationToken.None)
            .Returns(new CurrentUserDto(UserId, Email, "Cognito", Subject));
        MockSessionCoordinator.Capture().Returns(new AccountSessionLeaseModel(UserId, 2, CancellationToken.None));
        // Act
        var refresh = Sut.GetAccessTokenAsync(CancellationToken.None);
        var signOut = Sut.SignOutAsync(CancellationToken.None);
        refreshCompletion.SetResult(new OidcTokenResultModel(
            OidcFailureKindEnum.None,
            "refreshed-access",
            "rotated-refresh",
            Now.AddHours(1),
            null,
            null));
        await refresh;
        await signOut;
        // Assert
        Sut.CurrentSession.Should().BeNull();
        Received.InOrder(() =>
        {
            MockSessionCoordinator.TransitionAsync(UserId, CancellationToken.None);
            MockSessionCoordinator.TransitionAsync(UserId, CancellationToken.None);
            MockSessionCoordinator.TransitionAsync(null, CancellationToken.None);
        });
        await MockStore.Received(1).RemoveCredentialAsync();
        await MockCognitoSessionClient.Received(1).RevokeRefreshTokenAsync(
            "rotated-refresh",
            CancellationToken.None);
        await MockCognitoSessionClient.DidNotReceive().RevokeRefreshTokenAsync(
            RefreshToken,
            Arg.Any<CancellationToken>());
    }
}
