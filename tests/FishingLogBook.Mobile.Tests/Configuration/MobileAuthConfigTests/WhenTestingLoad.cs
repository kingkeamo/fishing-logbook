using AwesomeAssertions;
using FishingLogBook.Mobile.Configuration;

namespace FishingLogBook.Mobile.Tests.Configuration.MobileAuthConfigTests;

public class WhenTestingLoad
{
    [Fact]
    public void ItShouldLoadTheProvisionedDevelopmentNativeClientWithoutSecrets()
    {
        // Arrange
        // Act
        var config = MobileAuthConfig.Load();
        // Assert
        config.ClientId.Should().Be("513lhdbas693dqq2bs58v1ri8h");
        config.RedirectUri.Should().Be("com.fishinglogbook.mobile.demo://oauth2redirect");
        config.LogoutRedirectUri.Should().Be("com.fishinglogbook.mobile.demo://signedout");
        config.GetType().GetProperties().Should().NotContain(property =>
            property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }
}
