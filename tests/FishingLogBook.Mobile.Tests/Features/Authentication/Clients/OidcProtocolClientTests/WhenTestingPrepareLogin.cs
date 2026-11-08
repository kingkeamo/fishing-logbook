using AwesomeAssertions;
using FishingLogBook.Shared.Enums;

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

    [Theory]
    [InlineData(SignInProviderEnum.Google, "Google")]
    [InlineData(SignInProviderEnum.Apple, "SignInWithApple")]
    public async Task ItShouldPreselectTheFederatedProviderInTheAuthorizationRequest(SignInProviderEnum provider, string expected)
    {
        // Arrange
        var sut = CreateSut();
        // Act
        var result = await sut.PrepareLoginAsync(CancellationToken.None, provider);
        // Assert
        result.StartUrl.Should().Contain($"identity_provider={expected}");
        result.StartUrl.Should().Contain("code_challenge_method=S256");
        result.StartUrl.Should().Contain("resource=https%3A%2F%2Fapi.example.com");
    }

    [Fact]
    public async Task ItShouldNotPreselectAProviderForEmailPasswordSignIn()
    {
        // Arrange
        var sut = CreateSut();
        // Act
        var result = await sut.PrepareLoginAsync(CancellationToken.None, SignInProviderEnum.EmailPassword);
        // Assert
        result.StartUrl.Should().NotContain("identity_provider");
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
