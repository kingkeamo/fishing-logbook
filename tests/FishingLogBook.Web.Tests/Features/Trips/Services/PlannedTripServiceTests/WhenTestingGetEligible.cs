using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Trips.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.PlannedTripServiceTests;

public class WhenTestingGetEligible : BasePlannedTripServiceTest
{
    private static readonly Guid OtherTripId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Theory]
    [InlineData(FishingLogBook.Web.Common.SyncStatus.Synchronised)]
    [InlineData(FishingLogBook.Web.Common.SyncStatus.SavedLocally)]
    public async Task ItShouldExcludePreviouslySyncedPlansMissingFromTheAuthoritativeOnlineList(FishingLogBook.Web.Common.SyncStatus status)
    {
        // Arrange
        MockTripStore.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns([PlannedTrip(Now) with { SyncStatus = status, SyncedAt = Now.AddHours(-1) }]);

        // Act
        var result = await Sut.GetEligibleAsync(OwnerUserId, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        await MockTripClient.Received(1).GetPlannedAsync(null, Arg.Any<CancellationToken>());
        await MockTripStore.DidNotReceive().SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreserveOfflineEditsAndOfferAnUnsyncedNewPlan()
    {
        // Arrange
        var plan = PlannedTrip(Now) with { SyncStatus = FishingLogBook.Web.Common.SyncStatus.SavedLocally, Title = "Offline edit" };
        MockTripStore.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns([plan]);

        // Act
        var result = await Sut.GetEligibleAsync(OwnerUserId, CancellationToken.None);

        // Assert
        result.Should().ContainSingle(option => option.TripId == TripId && option.Title == "Offline edit");
        await MockTripStore.DidNotReceive().SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotOfferAServerPlanAlreadyActivatedLocallyWhileWaitingForSync()
    {
        // Arrange
        MockTripStore.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns([
            PlannedTrip(Now) with { Status = TripConstants.Active, SyncStatus = FishingLogBook.Web.Common.SyncStatus.SavedLocally }]);
        MockTripClient.GetPlannedAsync(null, Arg.Any<CancellationToken>()).Returns([
            new PlannedTripOptionDto(TripId, Now, OwnerUserId, "Me")]);

        // Act
        var result = await Sut.GetEligibleAsync(OwnerUserId, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        await MockTripClient.Received(1).GetPlannedAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNothingWithoutAViewer()
    {
        // Arrange

        // Act
        var result = await Sut.GetEligibleAsync(Guid.Empty, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        await MockTripClient.DidNotReceive().GetPlannedAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>());
        await MockTripStore.DidNotReceive().GetAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCallTheServerWhenOffline()
    {
        // Arrange
        MockNetwork.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        MockTripStore.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns([PlannedTrip(Now.AddDays(1))]);

        // Act
        var result = await Sut.GetEligibleAsync(OwnerUserId, CancellationToken.None);

        // Assert
        result.Should().ContainSingle(option => option.TripId == TripId && option.IsOrganiser);
        await MockTripClient.DidNotReceive().GetPlannedAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnOnlineLookupAndLogWhenTheServerFails()
    {
        // Arrange
        var failure = new HttpRequestException("down");
        MockTripClient.GetPlannedAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>()).ThrowsAsync(failure);
        MockTripStore.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns([PlannedTrip(Now)]);

        // Act
        var act = () => Sut.GetEligibleAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await MockLogging.Received(1).LogErrorAsync(
            "reading the planned trips",
            failure,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectALocalStorageFailureInsteadOfReportingNoPlans()
    {
        // Arrange
        MockTripClient.GetPlannedAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("down"));
        MockTripStore.GetAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("store"));

        // Act
        var act = () => Sut.GetEligibleAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await MockTripClient.DidNotReceive().GetPlannedAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPropagateCancellation()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        MockNetwork.IsOnlineAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException(source.Token));

        // Act
        var act = () => Sut.GetEligibleAsync(OwnerUserId, source.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(-3, 0, true)]
    [InlineData(3, 0, true)]
    [InlineData(-3, -1, false)]
    [InlineData(3, 1, false)]
    public async Task ItShouldKeepPlannedTripsInsideTheThreeDayWindowOnly(
        int days,
        int extraMinutes,
        bool expected)
    {
        // Arrange
        var plannedStart = Now.AddDays(days).AddMinutes(extraMinutes);
        MockTripStore.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns([PlannedTrip(plannedStart) with { SyncStatus = FishingLogBook.Web.Common.SyncStatus.SavedLocally }]);
        MockTripClient.GetPlannedAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns([new PlannedTripOptionDto(OtherTripId, plannedStart, GuestUserId, "Sam")]);

        // Act
        var result = await Sut.GetEligibleAsync(OwnerUserId, CancellationToken.None);

        // Assert
        result.Any().Should().Be(expected);
        result.Count.Should().Be(expected ? 2 : 0);
    }

    [Fact]
    public async Task ItShouldIgnoreLocalTripsThatAreNotPlannedOwnedAndScheduled()
    {
        // Arrange
        var activeTrip = PlannedTrip(Now) with { Status = TripConstants.Active };
        var cancelled = PlannedTrip(Now, tripId: OtherTripId) with { Status = TripConstants.Cancelled };
        var someoneElses = PlannedTrip(Now, ownerUserId: GuestUserId, tripId: Guid.NewGuid());
        var unscheduled = PlannedTrip(Now, tripId: Guid.NewGuid()) with { PlannedStartOn = null };
        MockTripStore.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns([activeTrip, cancelled, someoneElses, unscheduled]);

        // Act
        var result = await Sut.GetEligibleAsync(OwnerUserId, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldMergeServerAndLocalTripsOnceOrderedByPlannedStart()
    {
        // Arrange
        var serverOnly = new PlannedTripOptionDto(OtherTripId, Now.AddHours(5), GuestUserId, "Sam")
        {
            Title = "Shared",
            Role = TripParticipantConstants.Participant,
            Participants =
            [
                new PlannedTripParticipantDto(OwnerUserId, "Me", TripParticipantConstants.Accepted),
                new PlannedTripParticipantDto(Guid.NewGuid(), "Pat", TripParticipantConstants.Pending)
            ]
        };
        var duplicate = new PlannedTripOptionDto(TripId, Now.AddHours(8), OwnerUserId, "Me");
        MockTripClient.GetPlannedAsync(null, Arg.Any<CancellationToken>()).Returns([duplicate, serverOnly]);
        MockTripStore.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns([PlannedTrip(Now.AddHours(8)), PlannedTrip(Now.AddHours(1), tripId: Guid.NewGuid()) with { SyncStatus = FishingLogBook.Web.Common.SyncStatus.SavedLocally }]);

        // Act
        var result = await Sut.GetEligibleAsync(OwnerUserId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result.Select(option => option.PlannedStartOn).Should().BeInAscendingOrder();
        result.Count(option => option.TripId == TripId).Should().Be(1);
        var shared = result.Single(option => option.TripId == OtherTripId);
        shared.IsOrganiser.Should().BeFalse();
        shared.OrganiserName.Should().Be("Sam");
        shared.Guests.Should().HaveCount(2);
        shared.Guests.Single(guest => guest.DisplayName == "Pat").IsPending.Should().BeTrue();
        await MockTripClient.Received(1).GetPlannedAsync(null, Arg.Any<CancellationToken>());
    }
}
