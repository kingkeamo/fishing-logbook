using AwesomeAssertions;
using FishingLogBook.Domain.Config;

namespace FishingLogBook.Infrastructure.Tests.ObjectStorageConfigTests;

public class WhenTestingIsConfigured
{
    [Fact]
    public void ItShouldBeUnconfigured_WhenSecretsAreUserSecretPlaceholders()
    {
        // Arrange
        var config = new ObjectStorageConfig
        {
            ServiceUrl = "set in user secrets fishinglogbook-api",
            BucketName = "fishing-logbook-dev",
            AccessKeyId = "set in user secrets fishinglogbook-api",
            SecretAccessKey = "set in user secrets fishinglogbook-api"
        };

        // Act
        var configured = config.IsConfigured;

        // Assert
        configured.Should().BeFalse();
    }

    [Fact]
    public void ItShouldBeConfigured_WhenAllValuesAreProvided()
    {
        // Arrange
        var config = new ObjectStorageConfig
        {
            ServiceUrl = "https://example.r2.cloudflarestorage.com",
            BucketName = "fishing-logbook-dev",
            AccessKeyId = "access-key",
            SecretAccessKey = "secret-key"
        };

        // Act
        var configured = config.IsConfigured;

        // Assert
        configured.Should().BeTrue();
    }
}
