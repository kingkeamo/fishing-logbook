using System.Xml.Linq;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Presentation;

public sealed class WhenTestingActiveTripPresentation
{
    private static readonly string MobileRoot = FindMobileRoot();

    [Fact]
    public void ItShouldOfferTripActionsInEvenThreeColumnRows()
    {
        // Arrange
        var view = XDocument.Load(Path.Combine(
            MobileRoot,
            "Features",
            "Trips",
            "Components",
            "ActiveTripView",
            "ActiveTripView.xaml"));

        // Act
        var actions = view.Descendants().Single(element => (string?)element.Attribute("AutomationId") == "TripActions");
        var rows = actions.Elements().Where(element => element.Name.LocalName == "Grid").ToArray();
        var buttons = actions.Descendants().Where(element => element.Name.LocalName == "Button").ToArray();

        // Assert
        rows.Should().HaveCount(2);
        rows.Select(row => (string?)row.Attribute("ColumnDefinitions")).Should().AllBe("*,*,*");
        rows.Select(row => (string?)row.Attribute("ColumnSpacing")).Should().AllBe("8");
        buttons.Select(button => (string?)button.Attribute("AutomationId")).Should().Equal(
            "TripRecordCatch",
            "TripAddExistingCatch",
            "TripAddPhoto",
            "TripUpdate",
            "TripFinish");
        buttons.Select(button => (string?)button.Attribute("FlexLayout.Basis")).Should().Equal(null, null, null, null, null);
        buttons.Select(button => (string?)button.Attribute("Margin")).Should().Equal(null, null, null, null, null);
    }

    private static string FindMobileRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "src", "FishingLogBook.Mobile", "FishingLogBook.Mobile.csproj")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        return Path.Combine(directory!.FullName, "src", "FishingLogBook.Mobile");
    }
}
