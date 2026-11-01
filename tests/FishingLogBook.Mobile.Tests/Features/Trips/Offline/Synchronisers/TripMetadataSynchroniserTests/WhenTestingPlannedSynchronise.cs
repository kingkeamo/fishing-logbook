using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Features.Trips.Offline.Synchronisers;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Offline.Synchronisers.TripMetadataSynchroniserTests;

public sealed class WhenTestingPlannedSynchronise : BaseTripServiceTest
{
    [Fact]
    public async Task ItShouldKeepOfflineWorkPendingWhenTheRebasedWriteFails()
    {
        // Arrange
        var plan = await Planned.PlanAsync("Offline title", null, Now, CancellationToken.None);
        var operation = (await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None)).Single();
        MockClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns(
            ApiResultModel<TripDto>.Failed(FishingLogBook.Mobile.Api.Enums.ApiOutcomeEnum.Conflict),
            ApiResultModel<TripDto>.Failed(FishingLogBook.Mobile.Api.Enums.ApiOutcomeEnum.ServerError));
        MockClient.GetAsync(plan.Detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(
            ApiResultModel<TripDetailDto>.Succeeded(plan.Detail with { Trip = plan.Detail.Trip with { Status = TripConstants.Active } }));

        // Act
        var result = await Synchroniser.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Retry);
        result.Persist.Should().BeNull();
        var stored = (await TripStore.GetAsync(OwnerUserId, plan.Detail.Trip.Id, CancellationToken.None))!;
        stored.HasPendingMetadata.Should().BeTrue();
        stored.Detail.Trip.Title.Should().Be("Offline title");
        await MockClient.Received(2).UpsertAsync(Arg.Is<TripDto>(sent => sent.Id == plan.Detail.Trip.Id), Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task ItShouldRebaseOfflineMetadataOnTheAuthoritativeCompletedLifecycleAfterAConflict()
    {
        // Arrange
        var plan = await Planned.PlanAsync("Offline title", "Offline place", Now.AddHours(-1), CancellationToken.None);
        var operation = (await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None)).Single();
        var authoritative = new TripDetailDto(plan.Detail.Trip with
        {
            Status = TripConstants.Completed,
            StartedOn = Now.AddMinutes(-45),
            EndedOn = Now.AddMinutes(-5)
        })
        { Role = TripParticipantConstants.Owner };
        MockClient.GetAsync(plan.Detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(authoritative));
        MockClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns(
            ApiResultModel<TripDto>.Failed(FishingLogBook.Mobile.Api.Enums.ApiOutcomeEnum.Conflict),
            ApiResultModel<TripDto>.Succeeded(new TripDto(plan.Detail.Trip.Id, TripConstants.Completed, authoritative.Trip.StartedOn, authoritative.Trip.EndedOn)
            { OwnerUserId = OwnerUserId, PlannedStartOn = plan.Detail.Trip.PlannedStartOn }));

        // Act
        var result = await Synchroniser.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        await CompleteAsync(result);

        // Assert
        var stored = (await TripStore.GetAsync(OwnerUserId, plan.Detail.Trip.Id, CancellationToken.None))!;
        stored.Detail.Trip.Status.Should().Be(TripConstants.Completed);
        stored.Detail.Trip.StartedOn.Should().Be(authoritative.Trip.StartedOn);
        stored.Detail.Trip.PlannedStartOn.Should().Be(plan.Detail.Trip.PlannedStartOn);
        stored.Detail.Trip.Title.Should().Be("Offline title");
        stored.HasPendingMetadata.Should().BeFalse();
        await MockClient.Received(1).UpsertAsync(Arg.Is<TripDto>(sent => sent.Status == TripConstants.Completed
            && sent.StartedOn == authoritative.Trip.StartedOn && sent.Title == "Offline title" && sent.PlaceName == "Offline place"), Arg.Any<CancellationToken>());
    }

    private TripMetadataSynchroniser Synchroniser => new(TripStore, MockClient);

    private PlannedTripService Planned => new(TripStore, MockClient, TripSut, MockSessionCoordinator, MockConnectivity, Clock);

    private async Task CompleteAsync(FishingLogBook.Mobile.Common.Offline.Models.OutboxOperationResultModel result)
    {
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync();
        await result.Persist!(connection, transaction, CancellationToken.None);
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task ItShouldTakeTheServersLifecycleWhenItActivatedThePlanFirst()
    {
        // Arrange
        var plannedStart = Now.AddDays(1);
        var plan = await Planned.PlanAsync("Corrib", null, plannedStart, CancellationToken.None);
        var operation = (await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None)).Single();
        var serverStart = plannedStart.AddMinutes(7);
        MockClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns(call =>
            ApiResultModel<TripDto>.Succeeded(call.Arg<TripDto>() with { Status = TripConstants.Active, StartedOn = serverStart }));

        // Act
        var result = await Synchroniser.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        await CompleteAsync(result);

        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Completed);
        var stored = (await TripStore.GetAsync(OwnerUserId, plan.Detail.Trip.Id, CancellationToken.None))!;
        stored.Detail.Trip.Status.Should().Be(TripConstants.Active);
        stored.Detail.Trip.StartedOn.Should().Be(serverStart);
        stored.Detail.Trip.PlannedStartOn.Should().Be(plannedStart);
        stored.HasPendingMetadata.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldSendThePlannedStartAndTheActualStartOfAnActivatedPlan()
    {
        // Arrange
        var plannedStart = Now.AddHours(-30);
        var plan = await Planned.PlanAsync("Corrib", null, plannedStart, CancellationToken.None);
        await Planned.ActivateAsync(new FishingLogBook.Mobile.Features.Trips.Models.PlannedTripOptionModel(
            plan.Detail.Trip.Id, plannedStart, OwnerUserId, null, IsOrganiser: true), CancellationToken.None);
        var operation = (await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None)).Single();
        MockClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ApiResultModel<TripDto>.Succeeded(call.Arg<TripDto>()));

        // Act
        var result = await Synchroniser.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Completed);
        await MockClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(sent => sent.Id == plan.Detail.Trip.Id
                && sent.Status == TripConstants.Active
                && sent.StartedOn == Now
                && sent.PlannedStartOn == plannedStart),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSendAPlannedTripWithItsPlannedStart()
    {
        // Arrange
        var plannedStart = Now.AddDays(2);
        var plan = await Planned.PlanAsync("Corrib", null, plannedStart, CancellationToken.None);
        var operation = (await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None)).Single();
        MockClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ApiResultModel<TripDto>.Succeeded(call.Arg<TripDto>()));

        // Act
        var result = await Synchroniser.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Completed);
        await MockClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(sent => sent.Id == plan.Detail.Trip.Id
                && sent.Status == TripConstants.Planned
                && sent.StartedOn == plannedStart
                && sent.PlannedStartOn == plannedStart
                && sent.EndedOn == null),
            Arg.Any<CancellationToken>());
    }
}
