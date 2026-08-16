using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;

namespace FishingLogBook.Api.Tests.ConfigurationTests;

public class WhenTestingInvalidAuthority
{
    [Theory]
    [InlineData("http://cognito-idp.us-east-1.amazonaws.com/us-east-1_testpool")]
    [InlineData("cognito-idp.us-east-1.amazonaws.com/us-east-1_testpool")]
    public void ItShouldFailToStartWhenAuthorityIsNotAnAbsoluteHttpsUri(string authority)
    {
        // Arrange
        using var factory = new InvalidAuthorityApiFactory(authority);

        // Act
        var act = () => _ = factory.Services;

        // Assert
        act.Should().Throw<Exception>()
            .Which.ToString().Should().ContainAll("Auth:Authority", "HTTPS");
    }
}
