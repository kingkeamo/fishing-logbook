using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.ActiveTripServiceTests;

public class WhenTestingActivatePlanned : BaseActiveTripServiceTest
{
    private static readonly DateTimeOffset PlannedStart = DateTimeOffset.Parse("2026-09-12T06:30:00Z");

    private static TripModel Planned(Guid? ownerUserId = null)
    {
        return new TripModel(
            TripId,
            ownerUserId ?? OwnerUserId,
            TripConstants.Planned,
            PlannedStart,
            Title: "Corrib",
            SyncStatus: SyncStatus.Synchronised,
            PlannedStartOn: PlannedStart);
    }

    [Fact]
    public async Task ItShouldRejectATripThatIsMissing()
    {
        // Arrange
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);

        // Act
        var act = () => Sut.ActivatePlannedAsync(OwnerUserId, TripId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await MockTripStore.DidNotReceive().SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectATripThatIsNotPlanned()
    {
        // Arrange
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(ActiveTrip());

        // Act
        var act = () => Sut.ActivatePlannedAsync(OwnerUserId, TripId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await MockTripStore.DidNotReceive().SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectACancelledTrip()
    {
        // Arrange
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(Planned() with { Status = TripConstants.Cancelled });

        // Act
        var act = () => Sut.ActivatePlannedAsync(OwnerUserId, TripId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await MockTripStore.DidNotReceive().SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnActivationBySomeoneOtherThanTheOrganiser()
    {
        // Arrange
        MockTripStore.GetAsync(OtherUserId, TripId, Arg.Any<CancellationToken>()).Returns(Planned());

        // Act
        var act = () => Sut.ActivatePlannedAsync(OtherUserId, TripId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await MockTripStore.DidNotReceive().SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseToActivateWhileAnotherTripIsActive()
    {
        // Arrange
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(Planned());
        MockTripStore.GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(ActiveTrip(Guid.NewGuid()));

        // Act
        var act = () => Sut.ActivatePlannedAsync(OwnerUserId, TripId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TripAlreadyActiveException>();
        await MockTripStore.DidNotReceive().SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldActivateWithTheActualStartAndKeepThePlannedStart()
    {
        // Arrange
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(Planned());
        var before = DateTimeOffset.UtcNow;

        // Act
        var activated = await Sut.ActivatePlannedAsync(OwnerUserId, TripId, CancellationToken.None);

        // Assert
        activated.Status.Should().Be(TripConstants.Active);
        activated.PlannedStartOn.Should().Be(PlannedStart);
        activated.StartedOn.Should().BeOnOrAfter(before);
        activated.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        activated.Title.Should().Be("Corrib");
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(trip => trip.Id == TripId
                && trip.Status == TripConstants.Active
                && trip.PlannedStartOn == PlannedStart
                && trip.StartedOn != PlannedStart),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRememberTheActivatedTripAsTheActiveTrip()
    {
        // Arrange
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(Planned());
        var changes = 0;
        Sut.StateChanged += (_, _) => changes++;

        // Act
        var activated = await Sut.ActivatePlannedAsync(OwnerUserId, TripId, CancellationToken.None);
        var active = await Sut.GetActiveAsync(OwnerUserId, CancellationToken.None);

        // Assert
        active.Should().Be(activated);
        changes.Should().Be(1);
    }
}
