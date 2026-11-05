using AwesomeAssertions;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Tests.Navigation.RouteConstantsTests;

public class WhenTestingGetPath : BaseRouteConstantsTest
{
    [Fact]
    public void ItShouldKeepExactlyFivePrimaryDestinationsSeparateFromSecondaryRoutes()
    {
        // Arrange
        var expected = new[] { "//main/dashboard", "//main/catches", "//main/trips", "//main/import", "//main/settings" };
        // Act
        var paths = Primary.Select(RouteConstants.GetPath).ToArray();
        // Assert
        paths.Should().Equal(expected);
        Primary.Should().NotContain(MobileRouteEnum.Explore).And.NotContain(MobileRouteEnum.Showcase)
            .And.NotContain(MobileRouteEnum.CatchDetail).And.NotContain(MobileRouteEnum.PhotoPreview)
            .And.NotContain(MobileRouteEnum.About);
        RouteConstants.GetPath(MobileRouteEnum.Import).Should().Be("//main/import");
        RouteConstants.GetPath(MobileRouteEnum.About).Should().Be("about");
    }
}
