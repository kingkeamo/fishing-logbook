using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Handlers.AuthenticationCallbackHandlerTests;

public class WhenTestingCanForward : BaseAuthenticationCallbackHandlerTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a uri")]
    [InlineData("https://evil.test/native/oauth2redirect?code=code")]
    [InlineData("http://fishing-logbook-dev.pages.dev/native/oauth2redirect?code=code")]
    [InlineData("https://fishing-logbook-dev.pages.dev:8443/native/oauth2redirect?code=code")]
    [InlineData("https://fishing-logbook-dev.pages.dev/native/other?code=code")]
    [InlineData("com.fishinglogbook.mobile.demo://oauth2redirect?code=code")]
    public void ItShouldRejectUrisThatAreNotRegisteredAuthenticationCallbacks(string? callbackUri)
    {
        // Arrange
        // Act
        var result = Sut.CanForward(callbackUri);
        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(LoginCallback)]
    [InlineData(LogoutCallback)]
    public void ItShouldAcceptTheRegisteredAuthenticationCallbacks(string callbackUri)
    {
        // Arrange
        // Act
        var result = Sut.CanForward(callbackUri);
        // Assert
        result.Should().BeTrue();
    }
}
