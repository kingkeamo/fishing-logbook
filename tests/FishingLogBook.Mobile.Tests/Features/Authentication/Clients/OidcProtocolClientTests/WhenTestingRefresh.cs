using System.Net;
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
