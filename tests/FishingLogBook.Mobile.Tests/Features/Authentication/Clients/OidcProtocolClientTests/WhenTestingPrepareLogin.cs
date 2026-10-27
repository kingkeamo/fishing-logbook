using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Clients.OidcProtocolClientTests;

public class WhenTestingPrepareLogin : BaseOidcProtocolClientTest
{
    [Fact]
    public async Task ItShouldAcceptCognitoEndpointsOnTheConfiguredHostedUiDomain()
    {
        // Arrange
        var sut = CreateSut();
        // Act
        var result = await sut.PrepareLoginAsync(CancellationToken.None);
        // Assert
        result.StartUrl.Should().StartWith($"{HostedUiDomain}/oauth2/authorize?");
        result.StartUrl.Should().Contain("code_challenge_method=S256");
        result.StartUrl.Should().Contain("resource=https%3A%2F%2Fapi.example.com");
        result.State.Should().NotBeNullOrWhiteSpace();
        result.CodeVerifier.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ItShouldRejectEndpointsOutsideTheAuthorityAndHostedUiDomain()
    {
        // Arrange
        var sut = CreateSut("https://attacker.example.net");
        // Act
        var act = () => sut.PrepareLoginAsync(CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
