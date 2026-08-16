using AwesomeAssertions;
using FishingLogBook.Api.Configuration;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Tests.Common.TestSupport;

namespace FishingLogBook.Api.Tests.ConfigurationTests;

public class WhenTestingEnsureRequired
{
    [Theory]
    [InlineData("Authority", "Auth:Authority")]
    [InlineData("ClientId", "Auth:ClientId")]
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
            ClientId = TestJwt.ClientId,
            ApiResource = TestAuthConstants.ApiResource,
            ApiScope = TestAuthConstants.ApiScope
        };
    }
}
