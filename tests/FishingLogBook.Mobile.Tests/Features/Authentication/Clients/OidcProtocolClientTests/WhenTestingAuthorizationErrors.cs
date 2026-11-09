using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Clients.OidcProtocolClientTests;

public class WhenTestingAuthorizationErrors : BaseOidcProtocolClientTest
{
    private const string Redirect = "https://fishing-logbook-dev.pages.dev/native/oauth2redirect";

    private static PendingAuthorizationModel Pending()
    {
        return new PendingAuthorizationModel(
            "https://auth.example.com/oauth2/authorize",
            "expected",
            "verifier",
            Redirect,
            DateTimeOffset.UtcNow.AddMinutes(5));
    }

    [Fact]
    public async Task ItShouldTreatAUserDenialAsACancellationWithoutExchangingTokens()
    {
        // Arrange
        var exchanged = false;
        var sut = CreateSut(tokenEndpoint: _ =>
        {
            exchanged = true;
            return CreateTokenError(System.Net.HttpStatusCode.BadRequest, "invalid_grant");
        });

        // Act
        var result = await sut.CompleteLoginAsync(
            new Uri($"{Redirect}?error=access_denied&state=expected"),
            Pending(),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(OidcFailureKindEnum.Cancelled);
        exchanged.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldTreatAnyOtherAuthorizationErrorAsAnInvalidResponse()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.CompleteLoginAsync(
            new Uri($"{Redirect}?error=server_error&state=expected"),
            Pending(),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(OidcFailureKindEnum.InvalidResponse);
    }

    [Fact]
    public async Task ItShouldTreatARejectedCodeExchangeAsAnInvalidResponse()
    {
        // Arrange
        var sut = CreateSut(tokenEndpoint: _ => CreateTokenError(System.Net.HttpStatusCode.BadRequest, "invalid_grant"));

        // Act
        var result = await sut.CompleteLoginAsync(
            new Uri($"{Redirect}?code=abc&state=expected"),
            Pending(),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(OidcFailureKindEnum.InvalidResponse);
        result.AccessToken.Should().BeNullOrEmpty();
    }
}
