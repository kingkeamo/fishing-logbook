using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Features.Trips.Offline.Synchronisers;
using FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Offline.Synchronisers.TripMetadataSynchroniserTests;

public sealed class WhenTestingSynchronise : BaseTripServiceTest
{
    private TripMetadataSynchroniser Synchroniser => new(TripStore, MockClient);

    private async Task<(Guid TripId, FishingLogBook.Mobile.Common.Offline.Models.OutboxOperationModel Operation)> StartTripAsync(string? title = "Corrib")
    {
        var trip = await TripSut.StartAsync(title, " Lough Corrib ", CancellationToken.None);
        var operation = (await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None)).Single();
        return (trip.Detail.Trip.Id, operation);
    }

    private void AcceptUpserts()
    {
        MockClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var sent = call.Arg<TripDto>();
            return ApiResultModel<TripDto>.Succeeded(sent);
        });
    }

    [Fact]
    public async Task ItShouldCompleteWithoutCallingTheServerWhenTheTripIsAlreadySynchronised()
    {
        // Arrange
        var (tripId, operation) = await StartTripAsync();
        AcceptUpserts();
        var first = await Synchroniser.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        await CompleteAsync(first, tripId);
        MockClient.ClearReceivedCalls();
        // Act
        var second = await Synchroniser.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        second.Outcome.Should().Be(OutboxOperationOutcomeEnum.Completed);
        await MockClient.DidNotReceive().UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailTerminallyWithoutARequestWhenTheAccountCannotManageTheTrip()
    {
        // Arrange
        var detail = ServerTrip(Guid.NewGuid(), role: TripParticipantConstants.Participant, owner: Guid.NewGuid());
        var participantTrip = new FishingLogBook.Mobile.Features.Trips.Offline.Models.LocalTripRecordModel { UserId = OwnerUserId, Detail = detail };
        await TripStore.SaveAsync(OwnerLease, participantTrip, 0, CancellationToken.None);
        var operation = (await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None)).Single();
        // Act
        var result = await Synchroniser.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Terminal);
        result.ErrorCode.Should().Be("TripUnavailable");
        await MockClient.DidNotReceive().UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ApiOutcomeEnum.Unavailable, OutboxOperationOutcomeEnum.Retry)]
    [InlineData(ApiOutcomeEnum.ValidationFailed, OutboxOperationOutcomeEnum.Terminal)]
    [InlineData(ApiOutcomeEnum.SessionExpired, OutboxOperationOutcomeEnum.Paused)]
    public async Task ItShouldMapServerFailuresToRetryTerminalOrPausedAndKeepTheTripPending(ApiOutcomeEnum outcome, OutboxOperationOutcomeEnum expected)
    {
        // Arrange
        var (tripId, operation) = await StartTripAsync();
        MockClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDto>.Failed(outcome));
        // Act
        var result = await Synchroniser.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(expected);
        (await TripStore.GetAsync(OwnerUserId, tripId, CancellationToken.None))!.HasPendingMetadata.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldRetryWhenTheServerAnswersForADifferentTrip()
    {
        // Arrange
        var (_, operation) = await StartTripAsync();
        MockClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns(call =>
            ApiResultModel<TripDto>.Succeeded(call.Arg<TripDto>() with { Id = Guid.NewGuid() }));
        // Act
        var result = await Synchroniser.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Retry);
        result.ErrorCode.Should().Be("InvalidTripResponse");
    }

    [Fact]
    public async Task ItShouldKeepALaterLocalEditPendingWhenAnEarlierRevisionIsConfirmed()
    {
        // Arrange
        var (tripId, operation) = await StartTripAsync();
        AcceptUpserts();
        var result = await Synchroniser.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        var started = (await TripStore.GetAsync(OwnerUserId, tripId, CancellationToken.None))!;
        await TripSut.UpdateAsync(started, started.Detail.Trip with { Title = "Edited while syncing" }, CancellationToken.None);
        // Act
        await CompleteAsync(result, tripId);
        // Assert
        var stored = (await TripStore.GetAsync(OwnerUserId, tripId, CancellationToken.None))!;
        stored.SyncedRevision.Should().Be(1);
        stored.Revision.Should().Be(2);
        stored.HasPendingMetadata.Should().BeTrue();
        stored.Detail.Trip.Title.Should().Be("Edited while syncing");
    }

    [Fact]
    public async Task ItShouldSendTheTripWithItsClientIdAndTrimmedDetailsAndMarkItSynchronised()
    {
        // Arrange
        var (tripId, operation) = await StartTripAsync();
        AcceptUpserts();
        // Act
        var result = await Synchroniser.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        await CompleteAsync(result, tripId);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Completed);
        await MockClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(sent => sent.Id == tripId && sent.OwnerUserId == OwnerUserId && sent.Title == "Corrib"
                && sent.PlaceName == "Lough Corrib" && sent.Status == TripConstants.Active && sent.StartedOn == Now),
            Arg.Any<CancellationToken>());
        var stored = (await TripStore.GetAsync(OwnerUserId, tripId, CancellationToken.None))!;
        stored.HasPendingMetadata.Should().BeFalse();
        stored.SyncedRevision.Should().Be(1);
    }

    private async Task CompleteAsync(FishingLogBook.Mobile.Common.Offline.Models.OutboxOperationResultModel result, Guid tripId)
    {
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync();
        await result.Persist!(connection, transaction, CancellationToken.None);
        await transaction.CommitAsync();
        tripId.Should().NotBeEmpty();
    }
}
