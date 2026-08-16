using AwesomeAssertions;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Tests.DependencyInjection;

namespace FishingLogBook.Web.Tests.Configuration.AuthConfigTests;

public class WhenTestingIncompleteRegistration : BaseDependencyInjectionTest
{
    [Theory]
    [InlineData(nameof(AuthConfig.Authority), "Auth:Authority")]
    [InlineData(nameof(AuthConfig.ClientId), "Auth:ClientId")]
    [InlineData(nameof(AuthConfig.ApiResource), "Auth:ApiResource")]
    [InlineData(nameof(AuthConfig.ApiScope), "Auth:ApiScope")]
    public void ItShouldFailToBuildWhenRequiredAuthValueIsMissing(string propertyName, string configurationKey)
    {
        // Arrange
        var authConfig = CreateCompleteAuthConfig();
        typeof(AuthConfig).GetProperty(propertyName)!.SetValue(authConfig, string.Empty);

        // Act
        var act = () => CreateProvider(authConfig);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(configurationKey);
    }
}
