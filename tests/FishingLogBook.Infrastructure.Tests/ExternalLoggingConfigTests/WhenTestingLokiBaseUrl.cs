using AwesomeAssertions;
using FishingLogBook.Domain.Config;

namespace FishingLogBook.Infrastructure.Tests.ExternalLoggingConfigTests;

public class WhenTestingLokiBaseUrl
{
    [Fact]
    public void ItShouldStripTheLokiPushPath()
    {
        // Arrange
        var config = new ExternalLoggingConfig
        {
            Url = "https://logs-prod-042.grafana.net/loki/api/v1/push"
        };

        // Act
        var baseUrl = config.LokiBaseUrl;

        // Assert
        baseUrl.Should().Be("https://logs-prod-042.grafana.net");
    }

    [Fact]
    public void ItShouldLeaveABaseUrlUnchanged()
    {
        // Arrange
        var config = new ExternalLoggingConfig
        {
            Url = "https://logs-prod-042.grafana.net"
        };

        // Act
        var baseUrl = config.LokiBaseUrl;

        // Assert
        baseUrl.Should().Be("https://logs-prod-042.grafana.net");
    }
}
