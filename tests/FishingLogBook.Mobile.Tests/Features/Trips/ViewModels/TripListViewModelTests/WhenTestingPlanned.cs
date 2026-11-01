using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Navigation;
using FishingLogBook.Shared.Constants;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripListViewModelTests;

public sealed class WhenTestingPlanned : BaseTripListViewModelTest
{
    [Fact]
    public async Task ItShouldRouteAnOrdinaryTripToItsDetail()
    {
        // Arrange
        var id = Guid.NewGuid();
        ServeHistory(trips: Summary(id, TripConstants.Completed));
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        var route = Sut.RouteFor(id);

        // Assert
        route.Should().Be(MobileRouteEnum.TripDetail);
    }

    [Fact]
    public async Task ItShouldRouteAnUnknownTripToItsDetail()
    {
        // Arrange
        ServeHistory();
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        var route = Sut.RouteFor(Guid.NewGuid());

        // Assert
        route.Should().Be(MobileRouteEnum.TripDetail);
    }

    [Fact]
    public async Task ItShouldShowAPlannedTripByItsPlannedDateWithoutStats()
    {
        // Arrange
        var id = Guid.NewGuid();
        var plannedStart = Now.AddDays(2);
        ServeHistory(trips: Summary(id, TripConstants.Planned, startedOn: plannedStart) with { PlannedStartOn = plannedStart });

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        var item = Sut.Trips.Single();
        item.HasPlannedLifecycle.Should().BeTrue();
        item.ShowStats.Should().BeFalse();
        item.IsActive.Should().BeFalse();
        item.PlannedStatusText.Should().Be("Planned");
        item.ActionText.Should().Be("View plan");
        item.DateLabel.Should().Be(plannedStart.ToString("d MMM yyyy", Text.Culture));
        Sut.RouteFor(id).Should().Be(MobileRouteEnum.TripPlan);
    }

    [Fact]
    public async Task ItShouldShowACancelledTripAsCancelledAndNotOfferStartBecauseOfIt()
    {
        // Arrange
        var id = Guid.NewGuid();
        ServeHistory(trips: Summary(id, TripConstants.Cancelled));

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.Trips.Single().PlannedStatusText.Should().Be("Cancelled");
        Sut.CanStart.Should().BeTrue();
        Sut.RouteFor(id).Should().Be(MobileRouteEnum.TripPlan);
    }

    [Fact]
    public async Task ItShouldReportAFailureToStartAndReloadTheList()
    {
        // Arrange
        ServeHistory();
        await Sut.LoadAsync(CancellationToken.None);
        MockTrips.StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("active"));

        // Act
        var id = await Sut.StartAsync(CancellationToken.None);

        // Assert
        id.Should().BeNull();
        Sut.HasActionFailure.Should().BeTrue();
        Sut.ActionFailureText.Should().Be("The planned trip could not be started.");
        await MockTrips.Received(2).LoadAsync(Arg.Any<CancellationToken>());
    }
}
