using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.PlannedTripServiceTests;

public sealed class WhenTestingActivate : BasePlannedTripServiceTest
{
    private static readonly Guid SharedTripId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static PlannedTripOptionModel Option(Guid id, bool organiser)
    {
        return new PlannedTripOptionModel(id, Now, organiser ? OwnerUserId : FriendUserId, "Sam", organiser);
    }

    [Fact]
    public async Task ItShouldRefuseToActivateWhileAnotherOwnedTripIsActive()
    {
        // Arrange
        var plan = await Sut.PlanAsync("Corrib", null, Now, CancellationToken.None);
        await TripSut.StartAsync("Running", null, CancellationToken.None);

        // Act
        var act = () => Sut.ActivateAsync(Option(plan.Detail.Trip.Id, organiser: true), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await TripStore.GetAsync(OwnerUserId, plan.Detail.Trip.Id, CancellationToken.None))!
            .Detail.Trip.Status.Should().Be(TripConstants.Planned);
    }

    [Fact]
    public async Task ItShouldRefuseToActivateACancelledPlan()
    {
        // Arrange
        var plan = await Sut.PlanAsync("Corrib", null, Now, CancellationToken.None);
        await Sut.CancelPlanAsync(plan, CancellationToken.None);

        // Act
        var act = () => Sut.ActivateAsync(Option(plan.Detail.Trip.Id, organiser: true), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await TripStore.GetAsync(OwnerUserId, plan.Detail.Trip.Id, CancellationToken.None))!
            .Detail.Trip.Status.Should().Be(TripConstants.Cancelled);
    }

    [Fact]
    public async Task ItShouldFailWhenTheServerRejectsAParticipantActivation()
    {
        // Arrange
        MockClient.ActivateAsync(SharedTripId, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripDto>.Failed(ApiOutcomeEnum.ValidationFailed));

        // Act
        var act = () => Sut.ActivateAsync(Option(SharedTripId, organiser: false), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await MockClient.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldActivateAParticipantsTripOnTheServerAndHydrateItLocally()
    {
        // Arrange
        MockClient.ActivateAsync(SharedTripId, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripDto>.Succeeded(new TripDto(SharedTripId, TripConstants.Active, Now) { OwnerUserId = FriendUserId }));
        MockClient.GetAsync(SharedTripId, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripDetailDto>.Succeeded(ServerTrip(SharedTripId, TripConstants.Active, TripParticipantConstants.Participant, FriendUserId)));

        // Act
        var id = await Sut.ActivateAsync(Option(SharedTripId, organiser: false), CancellationToken.None);

        // Assert
        id.Should().Be(SharedTripId);
        await MockClient.Received(1).ActivateAsync(SharedTripId, Arg.Any<CancellationToken>());
        var stored = await TripStore.GetAsync(OwnerUserId, SharedTripId, CancellationToken.None);
        stored!.IsActive.Should().BeTrue();
        stored.CanContribute.Should().BeTrue();
        stored.CanManage.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldActivateTheOrganisersPlanOfflineKeepingThePlannedStartAndQueueingOneUpsert()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var plannedStart = Now.AddHours(-20);
        var plan = await Sut.PlanAsync("Corrib", null, plannedStart, CancellationToken.None);

        // Act
        var id = await Sut.ActivateAsync(Option(plan.Detail.Trip.Id, organiser: true), CancellationToken.None);

        // Assert
        id.Should().Be(plan.Detail.Trip.Id);
        var stored = await CreateTripStore().GetAsync(OwnerUserId, id, CancellationToken.None);
        stored!.Detail.Trip.Status.Should().Be(TripConstants.Active);
        stored.Detail.Trip.StartedOn.Should().Be(Now);
        stored.Detail.Trip.PlannedStartOn.Should().Be(plannedStart);
        stored.CanContribute.Should().BeTrue();
        var pending = await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None);
        pending.Should().ContainSingle(operation => operation.AggregateId == id
            && operation.OperationType == OutboxOperationTypeEnum.UpsertTripMetadata);
        await MockClient.DidNotReceive().ActivateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
