using AwesomeAssertions;
using FishingLogBook.Domain.Config;

namespace FishingLogBook.Infrastructure.Tests.ExternalLoggingConfigTests;

public class WhenTestingIsConfigured
{
    [Fact]
    public void ItShouldBeUnconfigured_WhenSecretsArePlaceholders()
    {
        // Arrange
        var config = new ExternalLoggingConfig
        {
            Provider = ExternalLoggingConfig.GrafanaCloudProvider,
            Url = "set in user secrets fishinglogbook-api",
            User = "set in user secrets fishinglogbook-api",
            ApiToken = "set in user secrets fishinglogbook-api"
        };

        // Act
        var configured = config.IsConfigured;

        // Assert
        configured.Should().BeFalse();
    }

    [Fact]
    public void ItShouldBeConfigured_WhenGrafanaValuesAreProvided()
    {
        // Arrange
        var config = new ExternalLoggingConfig
        {
            Provider = ExternalLoggingConfig.GrafanaCloudProvider,
            Url = "https://logs.example.test/loki/api/v1/push",
            User = "12345",
            ApiToken = "token"
        };

        // Act
        var configured = config.IsConfigured;

        // Assert
        configured.Should().BeTrue();
    }
}
