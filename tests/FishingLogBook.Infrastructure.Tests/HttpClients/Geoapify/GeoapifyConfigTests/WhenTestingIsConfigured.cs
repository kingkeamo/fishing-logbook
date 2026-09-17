using AwesomeAssertions;
using FishingLogBook.Domain.Config;

namespace FishingLogBook.Infrastructure.Tests.HttpClients.Geoapify.GeoapifyConfigTests;

public class WhenTestingIsConfigured
{
    [Theory]
    [InlineData("", "https://api.geoapify.com/")]
    [InlineData("set in user secrets fishinglogbook-api", "https://api.geoapify.com/")]
    [InlineData("key", "http://api.geoapify.com/")]
    [InlineData("key", "not-a-url")]
    public void ItShouldRejectIncompleteOrUnsafeConfiguration(string apiKey, string baseUrl)
    {
        // Arrange
        var config = new GeoapifyConfig
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl
        };

        // Act
        var configured = config.IsConfigured;

        // Assert
        configured.Should().BeFalse();
    }

    [Fact]
    public void ItShouldAcceptAKeyAndHttpsProviderUrl()
    {
        // Arrange
        var config = new GeoapifyConfig
        {
            ApiKey = "configured-key",
            BaseUrl = "https://api.geoapify.com/"
        };

        // Act
        var configured = config.IsConfigured;

        // Assert
        configured.Should().BeTrue();
    }
}
