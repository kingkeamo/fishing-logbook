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
