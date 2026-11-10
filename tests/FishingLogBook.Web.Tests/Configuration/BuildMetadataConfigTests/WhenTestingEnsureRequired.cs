using AwesomeAssertions;
using FishingLogBook.Web.Configuration;

namespace FishingLogBook.Web.Tests.Configuration.BuildMetadataConfigTests;

public class WhenTestingEnsureRequired
{
    private const string FullSha = "0123456789abcdef0123456789ABCDEF01234567";

    private static BuildMetadataConfig Config(string version, string sha, string environment)
    {
        return new BuildMetadataConfig { Version = version, Sha = sha, Environment = environment };
    }

    [Theory]
    [InlineData("", "abc", "dev")]
    [InlineData(" ", "abc", "dev")]
    [InlineData("1.2.3", "", "dev")]
    [InlineData("1.2.3", " ", "dev")]
    [InlineData("1.2.3", "abc", "")]
    [InlineData("1.2.3", "abc", " ")]
    public void ItShouldRejectMetadataWithAMissingVersionShaOrEnvironment(string version, string sha, string environment)
    {
        // Arrange
        var config = Config(version, sha, environment);

        // Act
        var act = config.EnsureRequired;

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("Build metadata is incomplete.");
    }

    [Theory]
    [InlineData("1.2", FullSha)]
    [InlineData("v1.2.3", FullSha)]
    [InlineData("1.2.3-beta", FullSha)]
    [InlineData("1.2.3", "abc123")]
    [InlineData("1.2.3", "0123456789abcdef0123456789ABCDEF0123456")]
    [InlineData("1.2.3", "0123456789abcdef0123456789ABCDEF012345678")]
    [InlineData("1.2.3", "gggggggggggggggggggggggggggggggggggggggg")]
    public void ItShouldRejectAProductionBuildWithoutASemanticVersionAndFullSha(string version, string sha)
    {
        // Arrange
        var config = Config(version, sha, "prod");

        // Act
        var act = config.EnsureRequired;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Production build metadata must contain a semantic version and full commit SHA.");
    }

    [Theory]
    [InlineData("prod")]
    [InlineData("PROD")]
    public void ItShouldAcceptAProductionBuildWithASemanticVersionAndFullSha(string environment)
    {
        // Arrange
        var config = Config("1.2.3", FullSha, environment);

        // Act
        var act = config.EnsureRequired;

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ItShouldAcceptANonProductionBuildWithAnyNonBlankVersionAndSha()
    {
        // Arrange
        var config = Config("local", "abc", "dev");

        // Act
        var act = config.EnsureRequired;

        // Assert
        act.Should().NotThrow();
    }
}
