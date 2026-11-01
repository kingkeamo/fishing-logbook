using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using TripListPage = FishingLogBook.Web.Features.Trips.Pages.TripList.TripList;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.TripListTests;

public class WhenTestingPlanned : BaseTripListTest
{
    private static readonly DateTimeOffset PlannedStart = DateTimeOffset.Parse("2026-09-12T06:30:00Z");

    [Fact]
    public async Task ItShouldOfferToPlanATrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(StoreWith(), ClientWith());

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-list-plan").GetAttribute("href").Should().Be("/trips/plan"));
        cut.Find("#trip-list-plan").TextContent.Should().Contain("Plan a trip");
    }

    [Fact]
    public async Task ItShouldShowAPlannedLocalTripAsPlannedWithoutStatsAndLinkToThePlanEditor()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var planned = LocalTrip(status: TripConstants.Planned, startedOn: PlannedStart, title: "Corrib") with
        {
            PlannedStartOn = PlannedStart
        };
        await using var context = CreateContext(StoreWith(planned), ClientWith());

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-list-status-{LocalTripId:D}").TextContent.Should().Contain("Planned"));
        cut.FindAll($"#trip-list-catches-{LocalTripId:D}").Should().BeEmpty();
        cut.FindAll($"#trip-list-photographs-{LocalTripId:D}").Should().BeEmpty();
        cut.FindAll($"#trip-list-notes-{LocalTripId:D}").Should().BeEmpty();
        cut.FindAll($"#trip-list-active-{LocalTripId:D}").Should().BeEmpty();
        cut.Find($"#trip-list-view-{LocalTripId:D}").GetAttribute("href").Should().Be($"/trips/{LocalTripId:D}/plan");
    }

    [Fact]
    public async Task ItShouldShowACancelledRemoteTripAsCancelled()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var cancelled = RemoteTrip(status: TripConstants.Cancelled, startedOn: PlannedStart) with
        {
            PlannedStartOn = PlannedStart
        };
        await using var context = CreateContext(StoreWith(), ClientWith(cancelled));

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-list-status-{RemoteTripId:D}").TextContent.Should().Contain("Cancelled"));
        cut.FindAll($"#trip-list-catches-{RemoteTripId:D}").Should().BeEmpty();
        cut.Find($"#trip-list-view-{RemoteTripId:D}").GetAttribute("href")
            .Should().Be($"/trips/{RemoteTripId:D}/plan");
    }

    [Fact]
    public async Task ItShouldNotShowAStatusChipForAnOrdinaryTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(StoreWith(LocalTrip(status: TripConstants.Completed)), ClientWith());

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#trip-list-catches-{LocalTripId:D}").Should().NotBeNull());
        cut.FindAll($"#trip-list-status-{LocalTripId:D}").Should().BeEmpty();
    }
}
