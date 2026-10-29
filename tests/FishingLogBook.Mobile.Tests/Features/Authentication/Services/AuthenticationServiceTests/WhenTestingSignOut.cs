using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;
using FishingLogBook.Mobile.Features.OfflineAccess.Stores;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Services.AuthenticationServiceTests;

public class WhenTestingSignOut : BaseAuthenticationServiceTest
{
    [Fact]
    public async Task ItShouldNotCompleteLogoutWhenOfflineReadinessCannotBeRevoked()
    {
        // Arrange
        await EstablishSessionAsync();
        MockOfflineCapability.RevokeAsync(CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Secure storage unavailable")));
        // Act
        var action = () => Sut.SignOutAsync(CancellationToken.None);
        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
        Sut.CurrentSession.Should().NotBeNull();
        await MockStore.DidNotReceive().RemoveCredentialAsync();
        await MockSessionCoordinator.DidNotReceive().TransitionAsync(null, CancellationToken.None);
        await MockCognitoSessionClient.DidNotReceive().RevokeRefreshTokenAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRemainIneligibleAfterRestartWhenCapabilityDisablingFailsDuringLogout()
    {
        // Arrange
        await EstablishSessionAsync();
        OfflineCapabilityModel? persisted = new(Subject, UserId, IsDeviceReady: true);
        var offlineStore = Substitute.For<ISecureOfflineCapabilityStore>();
        offlineStore.GetAsync(CancellationToken.None).Returns(_ => persisted);
        offlineStore.SaveAsync(Arg.Any<OfflineCapabilityModel>(), CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Secure write failed")));
        offlineStore.When(store => store.RemoveAsync(CancellationToken.None))
            .Do(_ => persisted = null);
        var capabilityService = new OfflineCapabilityService(offlineStore);
        MockOfflineCapability.RevokeAsync(CancellationToken.None)
            .Returns(_ => capabilityService.RevokeAsync(CancellationToken.None));
        // Act
        await Sut.SignOutAsync(CancellationToken.None);
        var restartedService = new OfflineCapabilityService(offlineStore);
        var readinessAfterRestart = await restartedService.GetReadyAsync(CancellationToken.None);
        // Assert
        Sut.CurrentSession.Should().BeNull();
        readinessAfterRestart.Should().BeNull();
        await MockStore.Received(1).RemoveCredentialAsync();
        await offlineStore.Received(1).RemoveAsync(CancellationToken.None);
    }

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
