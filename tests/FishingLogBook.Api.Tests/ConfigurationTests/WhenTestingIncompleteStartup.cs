using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;

namespace FishingLogBook.Api.Tests.ConfigurationTests;

public class WhenTestingIncompleteStartup
{
    [Theory]
    [InlineData("Auth:Authority")]
    [InlineData("Auth:ClientId")]
    [InlineData("Auth:ApiResource")]
    [InlineData("Auth:ApiScope")]
    public void ItShouldFailToStartWhenRequiredAuthSettingIsMissing(string omittedKey)
    {
        // Arrange
        using var factory = new IncompleteAuthApiFactory(omittedKey);

        // Act
        var act = () => _ = factory.Services;

        // Assert
        act.Should().Throw<Exception>()
            .Which.ToString().Should().Contain(omittedKey);
    }
}
