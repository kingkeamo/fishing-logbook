using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Clients.OidcProtocolClientTests;

public class WhenTestingCompleteLogin : BaseOidcProtocolClientTest
{
    [Theory]
    [InlineData("https://evil.test/native/oauth2redirect?code=code&state=expected")]
    [InlineData("com.fishinglogbook.mobile.demo://oauth2redirect?code=code&state=expected")]
    [InlineData("https://fishing-logbook-dev.pages.dev/native/oauth2redirect?code=code&state=wrong")]
    public async Task ItShouldRejectCallbackAndStateMismatchesBeforeTokenExchange(string callback)
    {
        // Arrange
        var sut = CreateSut();
        var pending = new PendingAuthorizationModel(
            "https://auth.example.com/oauth2/authorize",
            "expected",
            "verifier",
            "https://fishing-logbook-dev.pages.dev/native/oauth2redirect",
            DateTimeOffset.UtcNow.AddMinutes(5));
        // Act
        var result = await sut.CompleteLoginAsync(
            new Uri(callback),
            pending,
            CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(OidcFailureKindEnum.InvalidResponse);
    }
}
