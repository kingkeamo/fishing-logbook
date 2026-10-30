using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Services.AuthenticationServiceTests;

public sealed class WhenTestingOfflineRecovery : BaseAuthenticationServiceTest
{
    private readonly OfflineCapabilityModel _capability = new(Subject, UserId, IsDeviceReady: true);
    private readonly AccountSessionLeaseModel _lease = new(UserId, 7, CancellationToken.None);

    public WhenTestingOfflineRecovery()
    {
        MockOfflineSession.Current.Returns(_capability);
        MockSessionCoordinator.Capture().Returns(_lease);
        MockSessionCoordinator.IsCurrent(_lease).Returns(true);
        MockSessionCoordinator.TryCommitAsync(
                _lease,
                Arg.Any<Func<CancellationToken, Task>>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await call.Arg<Func<CancellationToken, Task>>()(call.ArgAt<CancellationToken>(2));
                return true;
            });
        MockStore.GetCredentialAsync()
            .Returns(new PersistedAuthenticationModel(RefreshToken, Subject, UserId));
    }

    [Fact]
    public async Task ItShouldPreserveTheLocalSessionAfterATemporaryRefreshFailure()
    {
        // Arrange
        MockRefreshService.RefreshAsync(RefreshToken, CancellationToken.None)
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.Transient));

        // Act
        var result = await Sut.InitializeAsync(CancellationToken.None);

        // Assert
        result.Should().Be(AuthenticationResultEnum.TemporarilyUnavailable);
        Sut.CurrentSession.Should().BeNull();
        MockOfflineSession.Current.Should().Be(_capability);
        MockOfflineSession.DidNotReceive().Lock();
        await MockOfflineCapability.DidNotReceive().RevokeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreserveTheLocalSessionWhenIdentityVerificationIsTemporarilyUnavailable()
    {
        // Arrange
        MockRefreshService.RefreshAsync(RefreshToken, CancellationToken.None).Returns(CreateTokenResult());
        MockCurrentUserClient.GetCurrentAsync(AccessToken, CancellationToken.None)
            .Returns(Task.FromException<CurrentUserDto>(new HttpRequestException("API unavailable")));

        // Act
        var result = await Sut.InitializeAsync(CancellationToken.None);

        // Assert
        result.Should().Be(AuthenticationResultEnum.StorageUnavailable);
        Sut.CurrentSession.Should().BeNull();
        MockOfflineSession.Current.Should().Be(_capability);
        MockOfflineSession.DidNotReceive().Lock();
        await MockOfflineCapability.DidNotReceive().RevokeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRevokeOnlyTheOfflineOwnerAfterPermanentRefreshRejection()
    {
        // Arrange
        MockRefreshService.RefreshAsync(RefreshToken, CancellationToken.None)
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.InvalidGrant));

        // Act
        var result = await Sut.InitializeAsync(CancellationToken.None);

        // Assert
        result.Should().Be(AuthenticationResultEnum.Invalid);
        await MockOfflineCapability.Received(1).RevokeIfOwnedByAsync(
            Subject,
            UserId,
            CancellationToken.None);
        await MockOfflineCapability.DidNotReceive().RevokeAsync(Arg.Any<CancellationToken>());
        await MockStore.Received(1).RemoveCredentialAsync();
    }

    [Fact]
    public async Task ItShouldNotRemoveANewerAccountsCredentialWhenTheLeaseChangesDuringRejectionCleanup()
    {
        // Arrange
        var rejectionCleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeRejectionCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PersistedAuthenticationModel? persisted = new(RefreshToken, Subject, UserId);
        var newerCredential = new PersistedAuthenticationModel("new-refresh", "new-subject", Guid.NewGuid());
        MockStore.GetCredentialAsync().Returns(_ => persisted);
        MockStore.When(store => store.RemoveCredentialAsync()).Do(_ => persisted = null);
        MockRefreshService.RefreshAsync(RefreshToken, CancellationToken.None)
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.InvalidGrant));
        MockOfflineCapability.RevokeIfOwnedByAsync(Subject, UserId, CancellationToken.None)
            .Returns(async _ =>
            {
                rejectionCleanupStarted.SetResult();
                await resumeRejectionCleanup.Task;
            });
        var recovery = Sut.InitializeAsync(CancellationToken.None);
        await rejectionCleanupStarted.Task;
        persisted = newerCredential;
        MockSessionCoordinator.IsCurrent(_lease).Returns(false);

        // Act
        resumeRejectionCleanup.SetResult();
        var result = await recovery;

        // Assert
        result.Should().Be(AuthenticationResultEnum.Invalid);
        persisted.Should().Be(newerCredential);
        await MockStore.DidNotReceive().RemoveCredentialAsync();
        MockOfflineSession.DidNotReceive().Lock();
    }

    [Fact]
    public async Task ItShouldFailClosedWhenTheStoredCredentialBelongsToAnotherAccount()
    {
        // Arrange
        MockStore.GetCredentialAsync()
            .Returns(new PersistedAuthenticationModel(RefreshToken, "other-subject", Guid.NewGuid()));

        // Act
        var result = await Sut.InitializeAsync(CancellationToken.None);

        // Assert
        result.Should().Be(AuthenticationResultEnum.Invalid);
        Sut.CurrentSession.Should().BeNull();
        await MockOfflineCapability.Received(1).RevokeIfOwnedByAsync(
            Subject,
            UserId,
            CancellationToken.None);
        await MockOfflineCapability.DidNotReceive().RevokeAsync(Arg.Any<CancellationToken>());
        await MockStore.DidNotReceive().RemoveCredentialAsync();
        await MockRefreshService.DidNotReceive().RefreshAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCommitALateRefreshAfterTheAccountLeaseChanges()
    {
        // Arrange
        var currentUserStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var currentUserCompletion = new TaskCompletionSource<CurrentUserDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        MockRefreshService.RefreshAsync(RefreshToken, CancellationToken.None).Returns(CreateTokenResult());
        MockCurrentUserClient.GetCurrentAsync(AccessToken, CancellationToken.None).Returns(_ =>
        {
            currentUserStarted.SetResult();
            return currentUserCompletion.Task;
        });
        var recovery = Sut.InitializeAsync(CancellationToken.None);
        await currentUserStarted.Task;
        MockSessionCoordinator.IsCurrent(_lease).Returns(false);

        // Act
        currentUserCompletion.SetResult(new CurrentUserDto(UserId, Email, "Cognito", Subject));
        var result = await recovery;

        // Assert
        result.Should().Be(AuthenticationResultEnum.Cancelled);
        Sut.CurrentSession.Should().BeNull();
        MockOfflineSession.DidNotReceive().Lock();
        await MockStore.DidNotReceive().SaveCredentialAsync(Arg.Any<PersistedAuthenticationModel>());
    }

    [Fact]
    public async Task ItShouldNotActivateRecoveryAfterLogoutIsRequested()
    {
        // Arrange
        var currentUserStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var currentUserCompletion = new TaskCompletionSource<CurrentUserDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        MockRefreshService.RefreshAsync(RefreshToken, CancellationToken.None).Returns(CreateTokenResult());
        MockCurrentUserClient.GetCurrentAsync(AccessToken, CancellationToken.None).Returns(_ =>
        {
            currentUserStarted.SetResult();
            return currentUserCompletion.Task;
        });
        var recovery = Sut.InitializeAsync(CancellationToken.None);
        await currentUserStarted.Task;
        var logout = Sut.SignOutAsync(CancellationToken.None);

        // Act
        currentUserCompletion.SetResult(new CurrentUserDto(UserId, Email, "Cognito", Subject));
        var recoveryResult = await recovery;
        await logout;

        // Assert
        recoveryResult.Should().Be(AuthenticationResultEnum.Cancelled);
        Sut.CurrentSession.Should().BeNull();
        await MockStore.DidNotReceive().SaveCredentialAsync(Arg.Any<PersistedAuthenticationModel>());
        await MockOfflineCapability.Received(1).RevokeAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldNotActivateAfterCancellationDuringSecureCredentialPersistence()
    {
        // Arrange
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saveCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        MockRefreshService.RefreshAsync(RefreshToken, cancellation.Token).Returns(CreateTokenResult());
        MockCurrentUserClient.GetCurrentAsync(AccessToken, cancellation.Token)
            .Returns(new CurrentUserDto(UserId, Email, "Cognito", Subject));
        MockStore.SaveCredentialAsync(Arg.Any<PersistedAuthenticationModel>()).Returns(_ =>
        {
            saveStarted.SetResult();
            return saveCompletion.Task;
        });
        MockSessionCoordinator.TryCommitAsync(
                _lease,
                Arg.Any<Func<CancellationToken, Task>>(),
                cancellation.Token)
            .Returns(async call =>
            {
                await call.Arg<Func<CancellationToken, Task>>()(cancellation.Token);
                return true;
            });
        var recovery = Sut.InitializeAsync(cancellation.Token);
        await saveStarted.Task;

        // Act
        cancellation.Cancel();
        saveCompletion.SetResult();
        var action = () => recovery;

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        Sut.CurrentSession.Should().BeNull();
        MockOfflineSession.DidNotReceive().Lock();
    }

    [Fact]
    public async Task ItShouldNotActivateWhenConnectivityIsLostDuringSecureCredentialPersistence()
    {
        // Arrange
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saveCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MockRefreshService.RefreshAsync(RefreshToken, CancellationToken.None).Returns(CreateTokenResult());
        MockCurrentUserClient.GetCurrentAsync(AccessToken, CancellationToken.None)
            .Returns(new CurrentUserDto(UserId, Email, "Cognito", Subject));
        MockStore.SaveCredentialAsync(Arg.Any<PersistedAuthenticationModel>()).Returns(_ =>
        {
            saveStarted.SetResult();
            return saveCompletion.Task;
        });
        var recovery = Sut.InitializeAsync(CancellationToken.None);
        await saveStarted.Task;
        MockConnectivity.IsOnline.Returns(false);
        MockConnectivity.TryRunWhileOnline(Arg.Any<Action>()).Returns(false);

        // Act
        saveCompletion.SetResult();
        var result = await recovery;

        // Assert
        result.Should().Be(AuthenticationResultEnum.Cancelled);
        Sut.CurrentSession.Should().BeNull();
        MockOfflineSession.DidNotReceive().Lock();
        MockConnectivity.Received(1).TryRunWhileOnline(Arg.Any<Action>());
    }

    [Fact]
    public async Task ItShouldNotActivateWhenConnectivityChangesAtTheFinalActivationBoundary()
    {
        // Arrange
        var activationReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeActivation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var online = true;
        MockRefreshService.RefreshAsync(RefreshToken, CancellationToken.None).Returns(CreateTokenResult());
        MockCurrentUserClient.GetCurrentAsync(AccessToken, CancellationToken.None)
            .Returns(new CurrentUserDto(UserId, Email, "Cognito", Subject));
        MockStore.SaveCredentialAsync(Arg.Any<PersistedAuthenticationModel>())
            .Returns(async _ => await Task.Yield());
        MockConnectivity.TryRunWhileOnline(Arg.Any<Action>()).Returns(call =>
        {
            activationReached.SetResult();
            resumeActivation.Task.GetAwaiter().GetResult();
            if (!online)
            {
                return false;
            }

            call.Arg<Action>()();
            return true;
        });
        var recovery = Sut.InitializeAsync(CancellationToken.None);
        await activationReached.Task;
        online = false;

        // Act
        resumeActivation.SetResult();
        var result = await recovery;

        // Assert
        result.Should().Be(AuthenticationResultEnum.Cancelled);
        Sut.CurrentSession.Should().BeNull();
        MockOfflineSession.DidNotReceive().Lock();
    }

    [Fact]
    public async Task ItShouldRollBackOnlineActivationWhenConnectivityIsLostImmediatelyAfterPublish()
    {
        // Arrange
        MockRefreshService.RefreshAsync(RefreshToken, CancellationToken.None).Returns(CreateTokenResult());
        MockCurrentUserClient.GetCurrentAsync(AccessToken, CancellationToken.None)
            .Returns(new CurrentUserDto(UserId, Email, "Cognito", Subject));
        MockConnectivity.TryRunWhileOnline(Arg.Any<Action>()).Returns(call =>
        {
            call.Arg<Action>()();
            return false;
        });

        // Act
        var result = await Sut.InitializeAsync(CancellationToken.None);

        // Assert
        result.Should().Be(AuthenticationResultEnum.Cancelled);
        Sut.CurrentSession.Should().BeNull();
        MockOfflineSession.Received(1).Lock();
        MockOfflineSession.Received(1).Activate(_capability);
    }

    [Fact]
    public async Task ItShouldVerifyAndCommitTheOfflineOwnerBeforeActivatingOnline()
    {
        // Arrange
        MockRefreshService.RefreshAsync(RefreshToken, CancellationToken.None).Returns(CreateTokenResult());
        MockCurrentUserClient.GetCurrentAsync(AccessToken, CancellationToken.None)
            .Returns(new CurrentUserDto(UserId, Email, "Cognito", Subject));

        // Act
        var result = await Sut.InitializeAsync(CancellationToken.None);

        // Assert
        result.Should().Be(AuthenticationResultEnum.Success);
        Sut.CurrentSession.Should().NotBeNull();
        Sut.CurrentSession!.Subject.Should().Be(Subject);
        Sut.CurrentSession.UserId.Should().Be(UserId);
        MockOfflineSession.Received(1).Lock();
        await MockSessionCoordinator.Received(1).TryCommitAsync(
            _lease,
            Arg.Any<Func<CancellationToken, Task>>(),
            CancellationToken.None);
        await MockStore.Received(1).SaveCredentialAsync(Arg.Is<PersistedAuthenticationModel>(value =>
            value.Subject == Subject && value.UserId == UserId));
    }
}
