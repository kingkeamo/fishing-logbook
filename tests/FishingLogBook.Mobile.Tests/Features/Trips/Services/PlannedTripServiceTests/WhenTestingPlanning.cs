using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Shared.Constants;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.PlannedTripServiceTests;

public sealed class WhenTestingPlanning : BasePlannedTripServiceTest
{
    [Fact]
    public async Task ItShouldRejectPlanningWithoutASignedInAccount()
    {
        // Arrange
        MockSessionCoordinator.Capture().Returns((FishingLogBook.Mobile.Features.Authentication.Models.AccountSessionLeaseModel?)null);

        // Act
        var act = () => Sut.PlanAsync("Corrib", null, Now.AddDays(1), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldRejectAnEmptyPlannedStart()
    {
        // Arrange
        // Act
        var act = () => Sut.PlanAsync("Corrib", null, default, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        (await TripStore.ListAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotUpdateAnActiveTrip()
    {
        // Arrange
        var active = await TripSut.StartAsync("Now", null, CancellationToken.None);

        // Act
        var act = () => Sut.UpdatePlanAsync(active, "Renamed", null, Now.AddDays(1), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldNotCancelAnActiveTrip()
    {
        // Arrange
        var active = await TripSut.StartAsync("Now", null, CancellationToken.None);

        // Act
        var act = () => Sut.CancelPlanAsync(active, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await TripStore.GetAsync(OwnerUserId, active.Detail.Trip.Id, CancellationToken.None))!
            .Detail.Trip.Status.Should().Be(TripConstants.Active);
    }

    [Fact]
    public async Task ItShouldUpdateThePlannedStartAndTitleOfAPlannedTrip()
    {
        // Arrange
        var plan = await Sut.PlanAsync("Corrib", null, Now.AddDays(1), CancellationToken.None);
        var newStart = Now.AddDays(2);

        // Act
        var updated = await Sut.UpdatePlanAsync(plan, "  Renamed  ", " Shore ", newStart, CancellationToken.None);

        // Assert
        var stored = await TripStore.GetAsync(OwnerUserId, plan.Detail.Trip.Id, CancellationToken.None);
        stored!.Detail.Trip.Title.Should().Be("Renamed");
        stored.Detail.Trip.PlaceName.Should().Be("Shore");
        stored.Detail.Trip.PlannedStartOn.Should().Be(newStart);
        stored.Detail.Trip.StartedOn.Should().Be(newStart);
        stored.Detail.Trip.Status.Should().Be(TripConstants.Planned);
        stored.Revision.Should().Be(updated.Revision);
        stored.HasPendingMetadata.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldCancelAPlannedTripAndKeepItsPlannedStart()
    {
        // Arrange
        var start = Now.AddDays(1);
        var plan = await Sut.PlanAsync("Corrib", null, start, CancellationToken.None);

        // Act
        var cancelled = await Sut.CancelPlanAsync(plan, CancellationToken.None);

        // Assert
        cancelled.IsCancelled.Should().BeTrue();
        var stored = await TripStore.GetAsync(OwnerUserId, plan.Detail.Trip.Id, CancellationToken.None);
        stored!.Detail.Trip.Status.Should().Be(TripConstants.Cancelled);
        stored.Detail.Trip.PlannedStartOn.Should().Be(start);
        stored.CanContribute.Should().BeFalse();
        stored.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldPlanATripOfflineWithoutTouchingTheServerAndQueueOneUpsert()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var start = Now.AddDays(1);

        // Act
        var plan = await Sut.PlanAsync("  Corrib weekend ", " Lough Corrib ", start, CancellationToken.None);

        // Assert
        var reopened = await CreateTripStore().GetAsync(OwnerUserId, plan.Detail.Trip.Id, CancellationToken.None);
        reopened!.Detail.Trip.Status.Should().Be(TripConstants.Planned);
        reopened.Detail.Trip.Title.Should().Be("Corrib weekend");
        reopened.Detail.Trip.PlaceName.Should().Be("Lough Corrib");
        reopened.Detail.Trip.PlannedStartOn.Should().Be(start);
        reopened.Detail.Trip.StartedOn.Should().Be(start);
        reopened.CanContribute.Should().BeFalse();
        reopened.IsActive.Should().BeFalse();
        var pending = await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None);
        pending.Should().ContainSingle(operation => operation.AggregateId == plan.Detail.Trip.Id
            && operation.OperationType == OutboxOperationTypeEnum.UpsertTripMetadata);
        await MockClient.DidNotReceive().UpsertAsync(Arg.Any<FishingLogBook.Shared.Dtos.TripDto>(), Arg.Any<CancellationToken>());
    }
}
