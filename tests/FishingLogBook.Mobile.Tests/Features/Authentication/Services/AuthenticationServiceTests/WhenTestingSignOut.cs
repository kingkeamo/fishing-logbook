using AwesomeAssertions;
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
        await MockStore.Received(1).RemovePendingAuthorizationAsync();
        await MockCognitoSessionClient.Received(1).RevokeRefreshTokenAsync(
            RefreshToken,
            CancellationToken.None);
        await MockCognitoSessionClient.Received(1).OpenLogoutAsync(CancellationToken.None);
    }
}
