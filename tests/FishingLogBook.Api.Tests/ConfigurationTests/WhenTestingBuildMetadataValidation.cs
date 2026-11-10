using AwesomeAssertions;
using FishingLogBook.Api.Configuration;

namespace FishingLogBook.Api.Tests.ConfigurationTests;

public class WhenTestingBuildMetadataValidation
{
    private const string FullSha = "0123456789abcdef0123456789abcdef01234567";

    [Theory]
    [InlineData("", FullSha, "prod")]
    [InlineData("1.2.3", "", "prod")]
    [InlineData("1.2.3", FullSha, "")]
    [InlineData("  ", FullSha, "dev")]
    public void ItShouldRejectMetadataWithAMissingField(string version, string sha, string environment)
    {
        // Arrange
        var config = new BuildMetadataConfig { Version = version, Sha = sha, Environment = environment };

        // Act
        var act = config.EnsureRequired;

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("Build metadata is incomplete.");
    }

    [Theory]
    [InlineData("1.2", FullSha)]
    [InlineData("v1.2.3", FullSha)]
    [InlineData("1.2.3-rc1", FullSha)]
    [InlineData("1.2.3", "0123456")]
    [InlineData("1.2.3", "zzzz456789abcdef0123456789abcdef01234567")]
    public void ItShouldRejectProductionMetadataWithoutASemanticVersionAndFullCommitSha(string version, string sha)
    {
        // Arrange
        var config = new BuildMetadataConfig { Version = version, Sha = sha, Environment = "PROD" };

        // Act
        var act = config.EnsureRequired;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Production build metadata must contain a semantic version and full commit SHA.");
    }

    [Fact]
    public void ItShouldAcceptCompleteProductionMetadata()
    {
        // Arrange
        var config = new BuildMetadataConfig { Version = "1.2.3", Sha = FullSha, Environment = "prod" };

        // Act
        var act = config.EnsureRequired;

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ItShouldExposeTheMetadataAsADto()
    {
        // Arrange
        var timestamp = DateTimeOffset.Parse("2026-08-17T08:00:00Z");
        var config = new BuildMetadataConfig
        {
            Version = "1.2.3",
            Sha = FullSha,
            Environment = "prod",
            Timestamp = timestamp
        };

        // Act
        var dto = config.ToDto();

        // Assert
        dto.Version.Should().Be("1.2.3");
        dto.Sha.Should().Be(FullSha);
        dto.Environment.Should().Be("prod");
        dto.BuiltOn.Should().Be(timestamp);
    }
}
