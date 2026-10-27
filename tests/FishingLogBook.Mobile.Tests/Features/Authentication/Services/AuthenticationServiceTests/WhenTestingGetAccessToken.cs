using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Services.AuthenticationServiceTests;

public class WhenTestingGetAccessToken : BaseAuthenticationServiceTest
{
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
