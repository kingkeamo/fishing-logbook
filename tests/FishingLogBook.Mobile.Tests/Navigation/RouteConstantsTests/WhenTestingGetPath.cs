using AwesomeAssertions;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Tests.Navigation.RouteConstantsTests;

public class WhenTestingGetPath : BaseRouteConstantsTest
{
    [Fact]
    public void ItShouldKeepExactlyFourPrimaryDestinationsSeparateFromSecondaryRoutes()
    {
        // Arrange
        var expected = new[] { "//main/catches", "//main/trips", "//main/explore", "//main/settings" };
        // Act
        var paths = Primary.Select(RouteConstants.GetPath).ToArray();
        // Assert
        paths.Should().Equal(expected);
        Primary.Should().NotContain(MobileRouteEnum.Showcase).And.NotContain(MobileRouteEnum.CatchDetail).And.NotContain(MobileRouteEnum.PhotoPreview);
    }
}
