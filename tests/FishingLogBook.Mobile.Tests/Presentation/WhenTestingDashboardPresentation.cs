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
        var popup = XDocument.Load(Path.Combine(Path.GetDirectoryName(path)!, "..", "Modals", "DashboardFilterModal.xaml"));
        var filters = popup.Descendants().Single(node => (string?)node.Attribute("AutomationId") == "DashboardFilterControls");

        // Assert
        nodes.Select(node => (string?)node.Attribute("AutomationId")).Should().NotContain("DashboardRecordCatch");
        page.ToString().Should().NotContain("DashboardOnlineOnly");
        nodes.Should().NotContain(node => (string?)node.Attribute("AutomationId") == "DashboardFilterControls");
        page.ToString().Should().NotContain("Text[DashboardHeaviest]");
        explore.Descendants().Should().Contain(node => (string?)node.Attribute("AutomationId") == "DashboardFilterToggle");
        explore.Descendants().Should().Contain(node => (string?)node.Attribute("AutomationId") == "DashboardFilteredTotals");
        filters.Descendants().Should().Contain(node => (string?)node.Attribute("AutomationId") == "DashboardSpeciesFilter");
        popup.Descendants().Should().Contain(node => (string?)node.Attribute("AutomationId") == "DashboardApply");
        popup.Descendants().Should().Contain(node => (string?)node.Attribute("AutomationId") == "DashboardReset");
        popup.Descendants().Should().Contain(node => (string?)node.Attribute("AutomationId") == "DashboardFilterClose");
        page.ToString().Should().Contain("{Binding CaughtOn}");
    }
}
