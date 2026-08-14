using AwesomeAssertions;
using FishingLogBook.Domain.Config;

namespace FishingLogBook.Infrastructure.Tests.ExternalLoggingConfigTests;

public class WhenTestingStreamEnvironment
{
    [Fact]
    public void ItShouldReturnLocalhost()
    {
        // Arrange
        var config = new ExternalLoggingConfig { Environment = "localhost" };

        // Act
        var streamEnvironment = config.StreamEnvironment;

        // Assert
        streamEnvironment.Should().Be("localhost");
    }

    [Fact]
    public void ItShouldReturnDev()
    {
        // Arrange
        var config = new ExternalLoggingConfig { Environment = "Dev" };

        // Act
        var streamEnvironment = config.StreamEnvironment;

        // Assert
        streamEnvironment.Should().Be("dev");
    }

    [Fact]
    public void ItShouldReturnUnknownWhenUnset()
    {
        // Arrange
        var config = new ExternalLoggingConfig();

        // Act
        var streamEnvironment = config.StreamEnvironment;

        // Assert
        streamEnvironment.Should().Be("unknown");
    }

    [Fact]
    public void ItShouldReturnUnknownWhenValueIsNotAllowed()
    {
        // Arrange
        var config = new ExternalLoggingConfig { Environment = "eamonn-laptop" };

        // Act
        var streamEnvironment = config.StreamEnvironment;

        // Assert
        streamEnvironment.Should().Be("unknown");
    }
}
