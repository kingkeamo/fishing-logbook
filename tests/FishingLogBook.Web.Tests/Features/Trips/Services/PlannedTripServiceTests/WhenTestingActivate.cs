using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.PlannedTripServiceTests;

public class WhenTestingActivate : BasePlannedTripServiceTest
{
    [Fact]
    public async Task ItShouldFailWhenTheOrganisersTripIsMissingEverywhere()
    {
        // Arrange
        var option = new PlannedTripOptionModel(TripId, Now, OwnerUserId, null, IsOrganiser: true);
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        MockTripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns((TripDetailDto?)null);

        // Act
        var act = () => Sut.ActivateAsync(OwnerUserId, option, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await MockActiveTrip.DidNotReceive().ActivatePlannedAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheServerReturnsNothingForAParticipant()
    {
        // Arrange
        var option = new PlannedTripOptionModel(TripId, Now, OwnerUserId, "Alex", IsOrganiser: false);
        MockTripClient.ActivateAsync(TripId, Arg.Any<CancellationToken>()).Returns((TripDto?)null);

        // Act
        var act = () => Sut.ActivateAsync(GuestUserId, option, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        MockActiveTrip.DidNotReceive().Invalidate();
    }

    [Fact]
    public async Task ItShouldActivateTheOrganisersLocalTripWithoutCallingTheServer()
    {
        // Arrange
        var option = new PlannedTripOptionModel(TripId, Now, OwnerUserId, null, IsOrganiser: true);
        var started = PlannedTrip(Now) with { Status = TripConstants.Active };
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(PlannedTrip(Now));
        MockActiveTrip.ActivatePlannedAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(started);

        // Act
        var result = await Sut.ActivateAsync(OwnerUserId, option, CancellationToken.None);

        // Assert
        result.Should().Be(started);
        await MockTripClient.DidNotReceive().ActivateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await MockTripClient.DidNotReceive().GetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await MockTripStore.DidNotReceive().SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
        await MockActiveTrip.Received(1).ActivatePlannedAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFetchAndStoreTheTripBeforeTheOrganiserActivatesWhenItIsNotLocal()
    {
        // Arrange
        var option = new PlannedTripOptionModel(TripId, Now, OwnerUserId, null, IsOrganiser: true);
        var detail = new TripDetailDto(new TripViewDto(TripId, OwnerUserId, TripConstants.Planned, Now)
        {
            PlannedStartOn = Now,
            Title = "Corrib"
        });
        var started = PlannedTrip(Now) with { Status = TripConstants.Active };
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        MockTripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(detail);
        MockActiveTrip.ActivatePlannedAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(started);

        // Act
        var result = await Sut.ActivateAsync(OwnerUserId, option, CancellationToken.None);

        // Assert
        result.Should().Be(started);
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(trip => trip.Id == TripId
                && trip.OwnerUserId == OwnerUserId
                && trip.Status == TripConstants.Planned
                && trip.PlannedStartOn == Now
                && trip.SyncStatus == SyncStatus.Synchronised),
            Arg.Any<CancellationToken>());
        await MockActiveTrip.Received(1).ActivatePlannedAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldActivateAParticipantsTripOnTheServerAndRefreshTheActiveTrip()
    {
        // Arrange
        var option = new PlannedTripOptionModel(TripId, Now, OwnerUserId, "Alex", IsOrganiser: false);
        var activeStart = Now.AddMinutes(10);
        MockTripClient.ActivateAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(new TripDto(TripId, TripConstants.Active, activeStart)
            {
                OwnerUserId = OwnerUserId,
                PlannedStartOn = Now,
                Title = "Corrib"
            });

        // Act
        var result = await Sut.ActivateAsync(GuestUserId, option, CancellationToken.None);

        // Assert
        result.Status.Should().Be(TripConstants.Active);
        result.StartedOn.Should().Be(activeStart);
        result.PlannedStartOn.Should().Be(Now);
        result.OwnerUserId.Should().Be(OwnerUserId);
        result.Origin.Should().Be(TripOriginEnum.Server);
        result.ParticipantUserIds.Should().Contain(GuestUserId);
        await MockTripClient.Received(1).ActivateAsync(TripId, Arg.Any<CancellationToken>());
        await MockActiveTrip.DidNotReceive().ActivatePlannedAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        MockActiveTrip.Received(1).Invalidate();
    }
}
