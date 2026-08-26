using AwesomeAssertions;
using FishingLogBook.Web.Features.OfflineAccess;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.OfflineReconnectReturnRouteTests;

public class WhenTestingResolve
{
    [Fact]
    public void ItShouldFallBackToCatchesWhenThePathIsNull()
    {
        // Arrange
        string? path = null;

        // Act
        var resolved = OfflineReconnectReturnRoute.Resolve(path);

        // Assert
        resolved.Should().Be("/catches");
    }

    [Fact]
    public void ItShouldFallBackToCatchesWhenThePathIsEmpty()
    {
        // Act
        var resolved = OfflineReconnectReturnRoute.Resolve(string.Empty);

        // Assert
        resolved.Should().Be("/catches");
    }

    [Fact]
    public void ItShouldMapTheOfflineCatchesRouteToCatches()
    {
        // Act
        var resolved = OfflineReconnectReturnRoute.Resolve("offline/catches");

        // Assert
        resolved.Should().Be("/catches");
    }

    [Fact]
    public void ItShouldMapTheOfflineRecordRouteToCatchesRecord()
    {
        // Act
        var resolved = OfflineReconnectReturnRoute.Resolve("offline/record");

        // Assert
        resolved.Should().Be("/catches/record");
    }

    [Fact]
    public void ItShouldTolerateALeadingSlashOnTheCapturedPath()
    {
        // Act
        var resolved = OfflineReconnectReturnRoute.Resolve("/offline/catches");

        // Assert
        resolved.Should().Be("/catches");
    }

    [Theory]
    [InlineData("https://evil.example.com")]
    [InlineData("http://evil.example.com/catches")]
    [InlineData("//evil.example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("offline/../authentication/login")]
    [InlineData("offline/catches/../../secret")]
    [InlineData("unrecognised/route")]
    public void ItShouldRejectUnsafeOrUnknownValuesAndFallBackToCatches(string unsafeValue)
    {
        // Act
        var resolved = OfflineReconnectReturnRoute.Resolve(unsafeValue);

        // Assert
        resolved.Should().Be("/catches");
    }
}
