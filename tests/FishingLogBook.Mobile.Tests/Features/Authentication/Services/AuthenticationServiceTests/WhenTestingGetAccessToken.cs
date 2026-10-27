using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Services.AuthenticationServiceTests;

public class WhenTestingGetAccessToken : BaseAuthenticationServiceTest
{
    private static readonly DateTimeOffset ExpiredAt = Now.AddSeconds(31);

    [Fact]
    public async Task ItShouldInvalidateTheSessionWhenRefreshWasRevoked()
    {
        // Arrange
        await EstablishSessionAsync(Now.AddSeconds(30));
        MockOidcClient.RefreshAsync(RefreshToken, CancellationToken.None)
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.InvalidGrant));
        var action = () => Sut.GetAccessTokenAsync(CancellationToken.None);
        // Act
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        // Assert
        Sut.CurrentSession.Should().BeNull();
        await MockStore.Received(1).RemoveCredentialAsync();
        await MockSessionCoordinator.Received(1).TransitionAsync(null, CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldInvalidateTheSessionWithoutRetryingWhenCognitoPermanentlyRejectsTheClient()
    {
        // Arrange
        await EstablishSessionAsync(Now.AddSeconds(30));
        MockOidcClient.RefreshAsync(RefreshToken, CancellationToken.None)
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.Rejected));
        var action = () => Sut.GetAccessTokenAsync(CancellationToken.None);
        // Act
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        // Assert
        Sut.CurrentSession.Should().BeNull();
        await MockOidcClient.Received(1).RefreshAsync(RefreshToken, CancellationToken.None);
        await MockStore.Received(1).RemoveCredentialAsync();
        await MockSessionCoordinator.Received(1).TransitionAsync(null, CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldFailFastWithoutContactingCognitoWhileAnExpiredSessionIsBackingOff()
    {
        // Arrange
        await EstablishSessionAsync(Now.AddSeconds(30));
        AdvanceTo(ExpiredAt);
        MockOidcClient.RefreshAsync(RefreshToken, CancellationToken.None)
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.Transient));
        var action = () => Sut.GetAccessTokenAsync(CancellationToken.None);
        // Act
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        AdvanceTo(ExpiredAt.AddSeconds(14));
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        // Assert
        Sut.CurrentSession.Should().NotBeNull();
        await MockOidcClient.Received(1).RefreshAsync(RefreshToken, CancellationToken.None);
        await MockStore.DidNotReceive().RemoveCredentialAsync();
    }

    [Theory]
    [InlineData(29, 2)]
    [InlineData(30, 3)]
    public async Task ItShouldDoubleTheBackoffAfterConsecutiveTransientRefreshFailures(
        int secondsAfterSecondFailure,
        int expectedRefreshAttempts)
    {
        // Arrange
        await EstablishSessionAsync(Now.AddSeconds(30));
        MockOidcClient.RefreshAsync(RefreshToken, CancellationToken.None)
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.Transient));
        var action = () => Sut.GetAccessTokenAsync(CancellationToken.None);
        AdvanceTo(ExpiredAt);
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        AdvanceTo(ExpiredAt.AddSeconds(15));
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        AdvanceTo(ExpiredAt.AddSeconds(15 + secondsAfterSecondFailure));
        // Act
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        // Assert
        await MockOidcClient.Received(expectedRefreshAttempts).RefreshAsync(RefreshToken, CancellationToken.None);
        await MockStore.DidNotReceive().RemoveCredentialAsync();
    }

    [Fact]
    public async Task ItShouldKeepUsingTheValidAccessTokenWhileRefreshIsBackingOff()
    {
        // Arrange
        await EstablishSessionAsync(Now.AddSeconds(30));
        var lease = new AccountSessionLeaseModel(UserId, 2, CancellationToken.None);
        MockSessionCoordinator.Capture().Returns(lease);
        MockOidcClient.RefreshAsync(RefreshToken, CancellationToken.None)
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.Transient));
        // Act
        var first = await Sut.GetAccessTokenAsync(CancellationToken.None);
        var second = await Sut.GetAccessTokenAsync(CancellationToken.None);
        // Assert
        first.AccessToken.Should().Be(AccessToken);
        second.AccessToken.Should().Be(AccessToken);
        await MockOidcClient.Received(1).RefreshAsync(RefreshToken, CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldBackOffWhenTheRefreshedOwnerCannotBeVerified()
    {
        // Arrange
        await EstablishSessionAsync(Now.AddSeconds(30));
        var lease = new AccountSessionLeaseModel(UserId, 2, CancellationToken.None);
        MockSessionCoordinator.Capture().Returns(lease);
        MockOidcClient.RefreshAsync(RefreshToken, CancellationToken.None)
            .Returns(new OidcTokenResultModel(
                OidcFailureKindEnum.None,
                "refreshed-access",
                "rotated-refresh",
                Now.AddHours(1),
                null,
                null));
        MockCurrentUserClient.GetCurrentAsync("refreshed-access", CancellationToken.None)
            .Returns(Task.FromException<CurrentUserDto>(new HttpRequestException("API unavailable")));
        // Act
        var first = await Sut.GetAccessTokenAsync(CancellationToken.None);
        var second = await Sut.GetAccessTokenAsync(CancellationToken.None);
        // Assert
        first.AccessToken.Should().Be(AccessToken);
        second.AccessToken.Should().Be(AccessToken);
        await MockOidcClient.Received(1).RefreshAsync(RefreshToken, CancellationToken.None);
        await MockStore.DidNotReceive().SaveCredentialAsync(Arg.Is<PersistedAuthenticationModel>(
            value => value.RefreshToken == "rotated-refresh"));
    }

    [Fact]
    public async Task ItShouldRetryRefreshOnceTheBackoffElapses()
    {
        // Arrange
        await EstablishSessionAsync(Now.AddSeconds(30));
        var lease = new AccountSessionLeaseModel(UserId, 2, CancellationToken.None);
        MockSessionCoordinator.Capture().Returns(lease);
        MockOidcClient.RefreshAsync(RefreshToken, CancellationToken.None)
            .Returns(
                OidcTokenResultModel.Failed(OidcFailureKindEnum.Transient),
                new OidcTokenResultModel(
                    OidcFailureKindEnum.None,
                    "refreshed-access",
                    "rotated-refresh",
                    ExpiredAt.AddHours(1),
                    null,
                    null));
        MockCurrentUserClient.GetCurrentAsync("refreshed-access", CancellationToken.None)
            .Returns(new CurrentUserDto(UserId, Email, "Cognito", Subject));
        AdvanceTo(ExpiredAt);
        var action = () => Sut.GetAccessTokenAsync(CancellationToken.None);
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        // Act
        AdvanceTo(ExpiredAt.AddSeconds(15));
        var credential = await Sut.GetAccessTokenAsync(CancellationToken.None);
        // Assert
        credential.AccessToken.Should().Be("refreshed-access");
        await MockOidcClient.Received(2).RefreshAsync(RefreshToken, CancellationToken.None);
        await MockStore.Received(1).SaveCredentialAsync(Arg.Is<PersistedAuthenticationModel>(
            value => value.RefreshToken == "rotated-refresh" && value.UserId == UserId));
    }

    [Fact]
    public async Task ItShouldSerializeConcurrentRefreshAndReturnOneRotatedCredential()
    {
        // Arrange
        await EstablishSessionAsync(Now.AddSeconds(30));
        var refreshed = new OidcTokenResultModel(
            OidcFailureKindEnum.None,
            "refreshed-access",
            "rotated-refresh",
            Now.AddHours(1),
            null,
            null);
        var refreshCompletion = new TaskCompletionSource<OidcTokenResultModel>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        MockOidcClient.RefreshAsync(RefreshToken, CancellationToken.None)
            .Returns(refreshCompletion.Task);
        MockCurrentUserClient.GetCurrentAsync("refreshed-access", CancellationToken.None)
            .Returns(new CurrentUserDto(UserId, Email, "Cognito", Subject));
        var lease = new AccountSessionLeaseModel(UserId, 2, CancellationToken.None);
        MockSessionCoordinator.Capture().Returns(lease);
        // Act
        var requests = Enumerable.Range(0, 12)
            .Select(_ => Sut.GetAccessTokenAsync(CancellationToken.None))
            .ToArray();
        refreshCompletion.SetResult(refreshed);
        var credentials = await Task.WhenAll(requests);
        // Assert
        credentials.Should().AllSatisfy(credential =>
        {
            credential.AccessToken.Should().Be("refreshed-access");
            credential.SessionLease.Should().Be(lease);
        });
        await MockOidcClient.Received(1).RefreshAsync(RefreshToken, CancellationToken.None);
        await MockStore.Received(1).SaveCredentialAsync(Arg.Is<PersistedAuthenticationModel>(
            value => value.RefreshToken == "rotated-refresh"));
    }
}
