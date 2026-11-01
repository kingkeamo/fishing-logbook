using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Localization;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Models.TripModelTests;

public sealed class WhenTestingProperties
{
    private static ActiveTripModel Active(string title = "", string place = "", string message = "")
    {
        return new ActiveTripModel(
            Trip(), new UiTextService(CultureInfo.GetCultureInfo("en-GB")), "1 Nov 2026", title, place, "09:00", "1h 5m", "Active trip", "icon_play.png", false,
            "1 catch recorded", "No photos", "2 notes", [], true, true, true, true, false, false, message);
    }

    [Fact]
    public void ItShouldCarryEveryDisplayValueOfTheActiveTrip()
    {
        // Arrange
        // Act
        var model = Active("Corrib", "Lough Corrib", "Oops");
        // Assert
        model.DateHeading.Should().Be("1 Nov 2026");
        model.StartedText.Should().Be("09:00");
        model.ElapsedText.Should().Be("1h 5m");
        model.StatusText.Should().Be("Active trip");
        model.StatusIcon.Should().Be("icon_play.png");
        model.CatchSummary.Should().Be("1 catch recorded");
        model.PhotographSummary.Should().Be("No photos");
        model.NoteSummary.Should().Be("2 notes");
        model.Text["TripActiveLabel"].Should().NotBeEmpty();
        model.Trip.Detail.Trip.Id.Should().Be(TripId);
    }

    [Fact]
    public void ItShouldReportWhichOptionalTextsOfTheActiveTripAreFilledIn()
    {
        // Arrange
        // Act
        var empty = Active();
        var filled = Active("Corrib", "Lough Corrib", "Oops");
        // Assert
        (empty.HasTitle, empty.HasPlace, empty.HasMessage).Should().Be((false, false, false));
        (filled.HasTitle, filled.HasPlace, filled.HasMessage).Should().Be((true, true, true));
    }

    [Fact]
    public void ItShouldDescribeATripListRow()
    {
        // Arrange
        // Act
        var plain = new TripListItemModel(TripId, "1 Nov 2026", null, null, false, false, false, "No catches", "No photos", "No notes", "View trip", "Active trip", "Shared", "Waiting");
        var full = plain with { Title = "Corrib", Place = "Lough Corrib", IsShared = true, IsActive = true, IsPending = true };
        // Assert
        (plain.HasTitle, plain.HasPlace).Should().Be((false, false));
        (full.HasTitle, full.HasPlace).Should().Be((true, true));
        (plain.DateLabel, plain.CatchesText, plain.PhotographsText, plain.NotesText, plain.ActionText, plain.StatusText, plain.SharedText, plain.PendingText)
            .Should().Be(("1 Nov 2026", "No catches", "No photos", "No notes", "View trip", "Active trip", "Shared", "Waiting"));
        (full.IsShared, full.IsActive, full.IsPending).Should().Be((true, true, true));
    }

    [Fact]
    public void ItShouldDescribeATripAssociationAndItsAnglers()
    {
        // Arrange
        var angler = new TripAnglerOptionModel(ViewerId, "Me", true);
        // Act
        var association = new TripAssociationModel(TripId, "Corrib", "Lough Corrib", Now, [angler]);
        // Assert
        (association.TripId, association.Title, association.PlaceName, association.StartedOn).Should().Be((TripId, "Corrib", "Lough Corrib", Now));
        association.Anglers.Should().ContainSingle().Which.Should().Be(angler);
    }

    [Fact]
    public void ItShouldDescribeTheEditRows()
    {
        // Arrange
        // Act
        var catchRow = new TripEditCatchModel(TripId, "Pike", null, "Remove Pike from this trip");
        var withMeasurements = catchRow with { Measurements = "2 kg" };
        var photograph = new TripPhotographRowModel(TripId, null, true, "Remove photo");
        // Assert
        (catchRow.HasMeasurements, withMeasurements.HasMeasurements).Should().Be((false, true));
        (catchRow.Label, catchRow.RemoveLabel, catchRow.Id).Should().Be(("Pike", "Remove Pike from this trip", TripId));
        (photograph.HasSource, (photograph with { Source = "p.jpg" }).HasSource).Should().Be((false, true));
        (photograph.CanRemove, photograph.RemoveLabel).Should().Be((true, "Remove photo"));
    }
}
