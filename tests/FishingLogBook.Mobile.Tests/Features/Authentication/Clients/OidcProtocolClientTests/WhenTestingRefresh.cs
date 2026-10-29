using System.Net;
using System.Text;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Clients.OidcProtocolClientTests;

public class WhenTestingRefresh : BaseOidcProtocolClientTest
{
    [Fact]
    public async Task ItShouldClassifyARevokedRefreshTokenAsAnInvalidGrant()
    {
        // Arrange
        var sut = CreateSut(tokenEndpoint: _ => CreateTokenError(HttpStatusCode.BadRequest, "invalid_grant"));
        // Act
        var result = await sut.RefreshAsync("refresh-token", CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(OidcFailureKindEnum.InvalidGrant);
    }

    [Theory]
    [InlineData("invalid_client")]
    [InlineData("invalid_request")]
    [InlineData("invalid_scope")]
    [InlineData("unauthorized_client")]
    [InlineData("unsupported_grant_type")]
    public async Task ItShouldClassifyPermanentCognitoErrorsAsRejected(string error)
    {
        // Arrange
        var sut = CreateSut(tokenEndpoint: _ => CreateTokenError(HttpStatusCode.BadRequest, error));
        // Act
        var result = await sut.RefreshAsync("refresh-token", CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(OidcFailureKindEnum.Rejected);
    }

    [Fact]
    public async Task ItShouldClassifyATemporarilyUnavailableCognitoErrorAsTransient()
    {
        // Arrange
        var sut = CreateSut(tokenEndpoint: _ => CreateTokenError(HttpStatusCode.ServiceUnavailable, "temporarily_unavailable"));
        // Act
        var result = await sut.RefreshAsync("refresh-token", CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(OidcFailureKindEnum.Transient);
    }

    [Fact]
    public async Task ItShouldClassifyAServerErrorAsTransient()
    {
        // Arrange
        var sut = CreateSut(tokenEndpoint: _ => CreateTokenError(HttpStatusCode.ServiceUnavailable, "server_error"));
        // Act
        var result = await sut.RefreshAsync("refresh-token", CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(OidcFailureKindEnum.Transient);
    }

    [Fact]
    public async Task ItShouldClassifyANonProtocolErrorAsTransient()
    {
        // Arrange
        var sut = CreateSut(tokenEndpoint: _ => CreateTokenError(HttpStatusCode.BadGateway, "upstream failure"));
        // Act
        var result = await sut.RefreshAsync("refresh-token", CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(OidcFailureKindEnum.Transient);
    }

    [Fact]
    public async Task ItShouldReturnTheRefreshedTokens()
    {
        // Arrange
        var sut = CreateSut(tokenEndpoint: _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"access_token":"new-access","refresh_token":"new-refresh","token_type":"Bearer","expires_in":3600}""",
                Encoding.UTF8,
                "application/json")
        });
        // Act
        var result = await sut.RefreshAsync("refresh-token", CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.AccessToken.Should().Be("new-access");
        result.RefreshToken.Should().Be("new-refresh");
    }

    [Fact]
    public async Task ItShouldNotClassifyAnUnknownOAuthResponseAsRetryable()
    {
        // Arrange
        var sut = CreateSut(tokenEndpoint: _ => CreateTokenError(HttpStatusCode.BadRequest, "unexpected_error"));
        // Act
        var result = await sut.RefreshAsync("refresh-token", CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(OidcFailureKindEnum.InvalidResponse);
    }

    [Fact]
    public async Task ItShouldClassifyAnHttpFailureWithoutAProtocolErrorAsTransient()
    {
        // Arrange
        var sut = CreateSut(tokenEndpoint: _ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        // Act
        var result = await sut.RefreshAsync("refresh-token", CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(OidcFailureKindEnum.Transient);
    }

    [Fact]
    public async Task ItShouldClassifyANetworkFailureAsTransient()
    {
        // Arrange
        var sut = CreateSut(tokenEndpoint: _ => throw new HttpRequestException("Network unavailable"));
        // Act
        var result = await sut.RefreshAsync("refresh-token", CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(OidcFailureKindEnum.Transient);
    }

    [Fact]
    public async Task ItShouldClassifyAnUnavailableDiscoveryDocumentAsTransient()
    {
        // Arrange
        var sut = CreateSut(discoveryAvailable: false);
        // Act
        var result = await sut.RefreshAsync("refresh-token", CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(OidcFailureKindEnum.Transient);
    }
}
