using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Offline.Stores.SqliteTripStoreTests;

public sealed class WhenTestingContributionsAndConfirm : BaseSqliteTripStoreTest
{
    [Fact]
    public async Task ItShouldRefuseAContributionForAnotherAccount()
    {
        // Arrange
        var note = NewNote(Guid.NewGuid()) with { UserId = OtherUserId };
        // Act
        var act = () => TripStore.SaveContributionAsync(OwnerLease, note, 0, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldRefuseAContributionEditedFromAStaleRevision()
    {
        // Arrange
        var note = NewNote(await SeedTripAsync());
        await TripStore.SaveContributionAsync(OwnerLease, note, 0, CancellationToken.None);
        // Act
        var act = () => TripStore.SaveContributionAsync(OwnerLease, note, 0, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ItShouldRefuseToMoveAContributionToAnotherTrip()
    {
        // Arrange
        var note = NewNote(await SeedTripAsync());
        var other = await SeedTripAsync();
        await TripStore.SaveContributionAsync(OwnerLease, note, 0, CancellationToken.None);
        // Act
        var act = () => TripStore.SaveContributionAsync(OwnerLease, note with { TripId = other }, 1, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ItShouldStopSavingAContributionWhenTheSessionEnded()
    {
        // Arrange
        RevokeSession();
        // Act
        var act = () => TripStore.SaveContributionAsync(OwnerLease, NewNote(Guid.NewGuid()), 0, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        (await PendingAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldQueueANoteOperationForANoteAndAPhotographOperationForAPhotograph()
    {
        // Arrange
        var tripId = await SeedTripAsync();
        var note = NewNote(tripId);
        var photograph = note with
        {
            Id = Guid.NewGuid(),
            Note = null,
            Photograph = new FishingLogBook.Shared.Dtos.TripPhotographViewDto(Guid.NewGuid(), "image/jpeg", Now) { ContributedByUserId = OwnerUserId },
            RelativeFilePath = "photos/a.jpg"
        };
        // Act
        await TripStore.SaveContributionAsync(OwnerLease, note, 0, CancellationToken.None);
        await TripStore.SaveContributionAsync(OwnerLease, photograph, 0, CancellationToken.None);
        // Assert
        var pending = await PendingAsync();
        pending.Should().Contain(operation => operation.AggregateId == note.Id && operation.OperationType == OutboxOperationTypeEnum.WriteTripNote
            && operation.AggregateType == OutboxAggregateTypeEnum.TripNote);
        pending.Should().Contain(operation => operation.AggregateId == photograph.Id && operation.OperationType == OutboxOperationTypeEnum.WriteTripPhotograph
            && operation.AggregateType == OutboxAggregateTypeEnum.TripPhotograph);
        (await TripStore.ListContributionsAsync(OwnerUserId, tripId, CancellationToken.None)).Should().HaveCount(2);
        (await TripStore.ListContributionsAsync(OtherUserId, tripId, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldConfirmATripAndKeepTheNewestSyncedRevision()
    {
        // Arrange
        var trip = NewTrip();
        await TripStore.SaveAsync(OwnerLease, trip, 0, CancellationToken.None);
        await TripStore.SaveAsync(OwnerLease, trip, 1, CancellationToken.None);
        var current = (await TripStore.GetAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None))!;
        // Act
        await RunAsync((connection, transaction) => TripStore.ConfirmAsync(connection, transaction, current, CancellationToken.None));
        await RunAsync((connection, transaction) => TripStore.ConfirmAsync(connection, transaction, current with { Revision = 1 }, CancellationToken.None));
        // Assert
        var confirmed = await TripStore.GetAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None);
        confirmed!.SyncedRevision.Should().Be(2);
        confirmed.HasPendingMetadata.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldConfirmAContributionAndKeepTheNewestSyncedRevision()
    {
        // Arrange
        var tripId = await SeedTripAsync();
        var note = NewNote(tripId);
        await TripStore.SaveContributionAsync(OwnerLease, note, 0, CancellationToken.None);
        await TripStore.SaveContributionAsync(OwnerLease, note, 1, CancellationToken.None);
        var current = (await TripStore.ListContributionsAsync(OwnerUserId, tripId, CancellationToken.None)).Single();
        // Act
        await RunAsync((connection, transaction) => TripStore.ConfirmContributionAsync(connection, transaction, current, CancellationToken.None));
        await RunAsync((connection, transaction) => TripStore.ConfirmContributionAsync(connection, transaction, current with { Revision = 1 }, CancellationToken.None));
        // Assert
        var confirmed = (await TripStore.ListContributionsAsync(OwnerUserId, tripId, CancellationToken.None)).Single();
        confirmed.SyncedRevision.Should().Be(2);
        confirmed.IsPending.Should().BeFalse();
    }
}
