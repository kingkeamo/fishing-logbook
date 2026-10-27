using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Services.AuthenticationServiceTests;

public class WhenTestingInitialize : BaseAuthenticationServiceTest
{
    [Fact]
    public async Task ItShouldFailClosedWithoutDeletingCredentialsWhenSecureStorageCannotBeRead()
    {
        // Arrange
        MockStore.GetPendingAuthorizationAsync()
            .Returns(Task.FromException<PendingAuthorizationModel?>(
                new InvalidOperationException("Secure storage unavailable")));
        // Act
        var result = await Sut.InitializeAsync(CancellationToken.None);
        // Assert
        result.Should().Be(AuthenticationResultEnum.StorageUnavailable);
        Sut.CurrentSession.Should().BeNull();
        await MockStore.DidNotReceive().RemoveCredentialAsync();
        await MockStore.DidNotReceive().SaveCredentialAsync(Arg.Any<PersistedAuthenticationModel>());
        await MockSessionCoordinator.Received(1).TransitionAsync(null, CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldRemoveExpiredPendingAuthorizationWithoutCallingOidc()
    {
        // Arrange
        MockStore.GetPendingAuthorizationAsync().Returns(CreatePending(Now));
        // Act
        var result = await Sut.InitializeAsync(CancellationToken.None);
        // Assert
        result.Should().Be(AuthenticationResultEnum.Success);
        Sut.HasPendingAuthorization.Should().BeFalse();
        await MockStore.Received(1).RemovePendingAuthorizationAsync();
        await MockOidcClient.DidNotReceive().RefreshAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearOnlyAuthenticationWhenTheRefreshTokenWasRevoked()
    {
        // Arrange
        var credential = new PersistedAuthenticationModel(RefreshToken, Subject, UserId);
        MockStore.GetCredentialAsync().Returns(credential);
        MockOidcClient.RefreshAsync(RefreshToken, CancellationToken.None)
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.InvalidGrant));
        // Act
        var result = await Sut.InitializeAsync(CancellationToken.None);
        // Assert
        result.Should().Be(AuthenticationResultEnum.Invalid);
        Sut.CurrentSession.Should().BeNull();
        await MockStore.Received(1).RemoveCredentialAsync();
        await MockSessionCoordinator.Received(1).TransitionAsync(null, CancellationToken.None);
        await MockCurrentUserClient.DidNotReceive().GetCurrentAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetainTheCredentialWhenRefreshIsTemporarilyUnavailable()
    {
        // Arrange
        var credential = new PersistedAuthenticationModel(RefreshToken, Subject, UserId);
        MockStore.GetCredentialAsync().Returns(credential);
        MockOidcClient.RefreshAsync(RefreshToken, CancellationToken.None)
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.Transient));
        // Act
        var result = await Sut.InitializeAsync(CancellationToken.None);
        // Assert
        result.Should().Be(AuthenticationResultEnum.TemporarilyUnavailable);
        await MockStore.DidNotReceive().RemoveCredentialAsync();
        await MockSessionCoordinator.DidNotReceive().TransitionAsync(
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRestoreTheBoundOwnerUsingARefreshedAccessToken()
    {
        // Arrange
        var credential = new PersistedAuthenticationModel(RefreshToken, Subject, UserId);
        var refreshed = new OidcTokenResultModel(
            OidcFailureKindEnum.None,
            "refreshed-access",
            "rotated-refresh",
            Now.AddHours(1),
            null,
            null);
        MockStore.GetCredentialAsync().Returns(credential);
        MockOidcClient.RefreshAsync(RefreshToken, CancellationToken.None).Returns(refreshed);
        MockCurrentUserClient.GetCurrentAsync("refreshed-access", CancellationToken.None)
            .Returns(new CurrentUserDto(UserId, Email, "Cognito", Subject));
        // Act
        var result = await Sut.InitializeAsync(CancellationToken.None);
        // Assert
        result.Should().Be(AuthenticationResultEnum.Success);
        Sut.CurrentSession!.RefreshToken.Should().Be("rotated-refresh");
        Sut.CurrentSession.UserId.Should().Be(UserId);
        await MockStore.Received(1).SaveCredentialAsync(Arg.Is<PersistedAuthenticationModel>(
            value => value.RefreshToken == "rotated-refresh" &&
                value.Subject == Subject &&
                value.UserId == UserId));
        await MockSessionCoordinator.Received(1).TransitionAsync(UserId, CancellationToken.None);
    }
}
