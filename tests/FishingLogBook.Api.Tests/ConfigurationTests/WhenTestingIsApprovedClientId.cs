using AwesomeAssertions;
using FishingLogBook.Api.Configuration;
using FishingLogBook.Api.Tests.TestSupport;

namespace FishingLogBook.Api.Tests.ConfigurationTests;

public class WhenTestingIsApprovedClientId
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(TestJwt.ArbitraryClientId)]
    [InlineData("TEST-PWA-CLIENT")]
    public void ItShouldRejectAClientIdThatIsNotExplicitlyApproved(string? clientId)
    {
        // Arrange
        var sut = CreateAuthConfig();

        // Act
        var result = sut.IsApprovedClientId(clientId);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(TestJwt.ClientId)]
    [InlineData(TestJwt.NativeClientId)]
    public void ItShouldAcceptEachExplicitlyApprovedClientId(string clientId)
    {
        // Arrange
        var sut = CreateAuthConfig();

        // Act
        var result = sut.IsApprovedClientId(clientId);

        // Assert
        result.Should().BeTrue();
    }

    private static AuthConfig CreateAuthConfig()
    {
        return new AuthConfig
        {
            ApprovedClientIds = [TestJwt.ClientId, TestJwt.NativeClientId]
        };
    }
}
