using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;

namespace FishingLogBook.Api.Tests.ConfigurationTests;

public class WhenTestingIncompleteStartup
{
    [Theory]
    [InlineData("Auth:Authority", "Auth:Authority")]
    [InlineData("Auth:ApprovedClientIds:0", "Auth:ApprovedClientIds")]
    [InlineData("Auth:ApiResource", "Auth:ApiResource")]
    [InlineData("Auth:ApiScope", "Auth:ApiScope")]
    public void ItShouldFailToStartWhenRequiredAuthSettingIsMissing(string omittedKey, string expectedConfigurationKey)
    {
        // Arrange
        using var factory = new IncompleteAuthApiFactory(omittedKey);

        // Act
        var act = () => _ = factory.Services;

        // Assert
        act.Should().Throw<Exception>()
            .Which.ToString().Should().Contain(expectedConfigurationKey);
    }
}
