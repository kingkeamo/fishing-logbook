using AwesomeAssertions;
using FishingLogBook.Tests.Common.TestSupport;
using FishingLogBook.Web.Configuration;

namespace FishingLogBook.Web.Tests.Configuration.AuthConfigTests;

public class WhenTestingEnsureRequired
{
    [Theory]
    [InlineData(nameof(AuthConfig.Authority), "Auth:Authority")]
    [InlineData(nameof(AuthConfig.ClientId), "Auth:ClientId")]
    [InlineData(nameof(AuthConfig.ApiResource), "Auth:ApiResource")]
    [InlineData(nameof(AuthConfig.ApiScope), "Auth:ApiScope")]
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
            Authority = "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_testpool",
            ClientId = "test-pwa-client",
            ApiResource = TestAuthConstants.ApiResource,
            ApiScope = TestAuthConstants.ApiScope
        };
    }
}
