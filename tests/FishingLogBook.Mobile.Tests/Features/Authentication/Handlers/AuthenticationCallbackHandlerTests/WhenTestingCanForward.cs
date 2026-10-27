using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Handlers.AuthenticationCallbackHandlerTests;

public class WhenTestingCanForward : BaseAuthenticationCallbackHandlerTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a uri")]
    [InlineData("https://evil.test/oauth2redirect?code=code")]
    [InlineData("com.fishinglogbook.mobile.demo://other?code=code")]
    [InlineData("other.application://oauth2redirect?code=code")]
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
