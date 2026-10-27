using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Factories;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Factories.NoRedirectHttpMessageHandlerFactoryTests;

public class WhenTestingCreate
{
    [Fact]
    public void ItShouldDisableAutomaticRedirectsToPreventBearerForwarding()
    {
        // Arrange
        // Act
        using var handler = NoRedirectHttpMessageHandlerFactory.Create();
        // Assert
        handler.AllowAutoRedirect.Should().BeFalse();
    }
}
