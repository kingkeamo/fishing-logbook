using AwesomeAssertions;
using FishingLogBook.Api.Configuration;

namespace FishingLogBook.Api.Tests.ConfigurationTests;

public class WhenTestingBuildMetadataConfig
{
    [Fact]
    public void ItShouldRejectIncompleteProductionMetadata()
    {
        var config = new BuildMetadataConfig
        {
            Version = "0.0.0-local",
            Sha = "local",
            Environment = "prod"
        };

        Action act = config.EnsureRequired;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItShouldAllowDevelopmentMetadataWithoutAReleaseTag()
    {
        var config = new BuildMetadataConfig
        {
            Version = "0.0.0-dev.42",
            Sha = "feature-sha",
            Environment = "dev"
        };

        config.Invoking(value => value.EnsureRequired()).Should().NotThrow();
    }
}
