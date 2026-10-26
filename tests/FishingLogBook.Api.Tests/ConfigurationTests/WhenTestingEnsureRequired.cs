using AwesomeAssertions;
using FishingLogBook.Api.Configuration;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Tests.Common.TestSupport;

namespace FishingLogBook.Api.Tests.ConfigurationTests;

public class WhenTestingEnsureRequired
{
    [Theory]
    [InlineData("Authority", "Auth:Authority")]
    [InlineData("ApiResource", "Auth:ApiResource")]
    [InlineData("ApiScope", "Auth:ApiScope")]
    public void ItShouldThrowWhenRequiredValueIsMissing(string propertyName, string configurationKey)
    {
        // Arrange
        var authConfig = CreateCompleteAuthConfig();
        typeof(AuthConfig).GetProperty(propertyName)!.SetValue(authConfig, " ");

        // Act
        var act = authConfig.EnsureRequired;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(configurationKey);
    }

    [Fact]
    public void ItShouldThrowWhenNoApprovedClientIdsAreConfigured()
    {
        // Arrange
        var authConfig = CreateCompleteAuthConfig();
        authConfig.ApprovedClientIds = [];

        // Act
        var act = authConfig.EnsureRequired;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Auth:ApprovedClientIds");
    }

    [Fact]
    public void ItShouldThrowWhenAnApprovedClientIdIsEmpty()
    {
        // Arrange
        var authConfig = CreateCompleteAuthConfig();
        authConfig.ApprovedClientIds = [TestJwt.ClientId, " "];

        // Act
        var act = authConfig.EnsureRequired;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().ContainAll("Auth:ApprovedClientIds", "empty");
    }

    [Fact]
    public void ItShouldThrowWhenApprovedClientIdsContainDuplicates()
    {
        // Arrange
        var authConfig = CreateCompleteAuthConfig();
        authConfig.ApprovedClientIds = [TestJwt.ClientId, TestJwt.ClientId];

        // Act
        var act = authConfig.EnsureRequired;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().ContainAll("Auth:ApprovedClientIds", "duplicate");
    }

    [Theory]
    [InlineData("http://cognito-idp.us-east-1.amazonaws.com/us-east-1_testpool")]
    [InlineData("http://127.0.0.1/us-east-1_testpool")]
    [InlineData("cognito-idp.us-east-1.amazonaws.com/us-east-1_testpool")]
    [InlineData("https://")]
    public void ItShouldThrowWhenAuthorityIsNotAnAbsoluteHttpsUri(string authority)
    {
        // Arrange
        var authConfig = CreateCompleteAuthConfig();
        authConfig.Authority = authority;

        // Act
        var act = authConfig.EnsureRequired;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().ContainAll("Auth:Authority", "HTTPS");
    }

    [Fact]
    public void ItShouldNotThrowWhenAllRequiredValuesArePresent()
    {
        // Arrange
        var authConfig = CreateCompleteAuthConfig();

        // Act
        var act = authConfig.EnsureRequired;

        // Assert
        act.Should().NotThrow();
    }

    private static AuthConfig CreateCompleteAuthConfig()
    {
        return new AuthConfig
        {
            Authority = TestJwt.Issuer,
            ApprovedClientIds = [TestJwt.ClientId, TestJwt.NativeClientId],
            ApiResource = TestAuthConstants.ApiResource,
            ApiScope = TestAuthConstants.ApiScope
        };
    }
}
