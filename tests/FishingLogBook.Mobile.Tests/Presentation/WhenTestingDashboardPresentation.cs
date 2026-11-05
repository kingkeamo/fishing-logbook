using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Presentation;

public class WhenTestingDashboardPresentation
{
    [Fact]
    public void ItShouldKeepFiltersAndFilteredCardsInExploreWithoutTheRedundantRecordButtonOrFooter()
    {
        // Arrange
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FishingLogBook.sln")))
        {
            directory = directory.Parent;
        }
        var path = Path.Combine(directory!.FullName, "src", "FishingLogBook.Mobile", "Features", "Dashboard", "Pages", "DashboardPage.xaml");

        // Act
        var page = XDocument.Load(path);
        var nodes = page.Descendants().ToArray();
        var explore = nodes.Single(node => (string?)node.Attribute("AutomationId") == "DashboardExplore");
        var filters = nodes.Single(node => (string?)node.Attribute("AutomationId") == "DashboardFilterControls");

        // Assert
        nodes.Select(node => (string?)node.Attribute("AutomationId")).Should().NotContain("DashboardRecordCatch");
        page.ToString().Should().NotContain("DashboardOnlineOnly");
        filters.Attribute("IsVisible")!.Value.Should().Be("{Binding IsFiltersExpanded}");
        explore.Descendants().Should().Contain(node => (string?)node.Attribute("AutomationId") == "DashboardFilterToggle");
        explore.Descendants().Should().Contain(node => (string?)node.Attribute("AutomationId") == "DashboardFilteredTotals");
        filters.Descendants().Should().Contain(node => (string?)node.Attribute("AutomationId") == "DashboardApply");
        filters.Descendants().Should().Contain(node => (string?)node.Attribute("AutomationId") == "DashboardReset");
        page.ToString().Should().Contain("{Binding CaughtOn}");
    }
}
