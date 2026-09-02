using AwesomeAssertions;
using Dapper;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripNoteRepositoryTests;

public class WhenTestingUpsert : BaseTripNoteRepositoryTest
{
    public WhenTestingUpsert(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldRejectANoteForATripThatDoesNotExist()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var saved = await Sut.UpsertAsync(NewNote(Guid.NewGuid(), userId), CancellationToken.None);

        // Assert
        saved.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotMoveANoteToAnotherTrip()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var first = await CreateTripAsync(userId, TripStatusEnum.Completed);
        var second = await CreateTripAsync(userId);
        var note = NewNote(first.Id, userId);
        await Sut.UpsertAsync(note, CancellationToken.None);

        // Act
        await Sut.UpsertAsync(NewNote(second.Id, userId, noteId: note.Id), CancellationToken.None);

        // Assert
        var stored = await Sut.GetByIdAsync(note.Id, CancellationToken.None);
        stored.Value!.TripId.Should().Be(first.Id);
    }

    [Fact]
    public async Task ItShouldReplayTheSameNoteWithoutDuplicatingIt()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var note = NewNote(trip.Id, userId);
        await Sut.UpsertAsync(note, CancellationToken.None);

        // Act
        var replay = await Sut.UpsertAsync(note, CancellationToken.None);

        // Assert
        replay.IsSuccess.Should().BeTrue();
        var stored = await Sut.GetByTripIdAsync(trip.Id, CancellationToken.None);
        stored.Value.Should().ContainSingle();
        stored.Value[0].Text.Should().Be(note.Text);
    }

    [Fact]
    public async Task ItShouldRoundTripTheRecordedInstant()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var recordedOn = StartedOn.AddMinutes(37).AddSeconds(12);

        // Act
        var saved = await Sut.UpsertAsync(
            NewNote(trip.Id, userId, recordedOn: recordedOn),
            CancellationToken.None);

        // Assert
        saved.Value.RecordedOn.Should().Be(recordedOn);
        var reloaded = await Sut.GetByIdAsync(saved.Value.Id, CancellationToken.None);
        reloaded.Value!.RecordedOn.Should().Be(recordedOn);
    }

    [Fact]
    public async Task ItShouldNotLetTheServerReplaceTheRecordedInstant()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var recordedOn = StartedOn.AddMinutes(15);

        // Act
        var saved = await Sut.UpsertAsync(
            NewNote(trip.Id, userId, recordedOn: recordedOn),
            CancellationToken.None);

        // Assert
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var row = await connection.QuerySingleAsync<(DateTimeOffset RecordedOn, DateTimeOffset CreatedOn)>(
            """SELECT recordedon, createdon FROM tripnotes WHERE id = @Id;""",
            new { saved.Value.Id });
        row.RecordedOn.Should().Be(recordedOn);
        row.CreatedOn.Should().NotBe(recordedOn);
    }

    [Fact]
    public async Task ItShouldStoreANoteAtTheLengthCap()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var text = new string('a', TripConstants.MaxNoteTextLength);

        // Act
        var saved = await Sut.UpsertAsync(NewNote(trip.Id, userId, text), CancellationToken.None);

        // Assert
        saved.IsSuccess.Should().BeTrue();
        saved.Value.Text.Should().HaveLength(TripConstants.MaxNoteTextLength);
    }

    [Fact]
    public async Task ItShouldStoreManyNotesForOneTripInRecordedOrder()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var third = NewNote(trip.Id, userId, "wind picked up", recordedOn: StartedOn.AddHours(5));
        var first = NewNote(trip.Id, userId, "fish rising near the reeds", recordedOn: StartedOn.AddHours(1));
        var second = NewNote(trip.Id, userId, "changed to olive nymph", recordedOn: StartedOn.AddHours(3));

        // Act
        await Sut.UpsertAsync(third, CancellationToken.None);
        await Sut.UpsertAsync(first, CancellationToken.None);
        await Sut.UpsertAsync(second, CancellationToken.None);

        // Assert
        var stored = await Sut.GetByTripIdAsync(trip.Id, CancellationToken.None);
        stored.Value.Select(note => note.Id).Should().Equal(first.Id, second.Id, third.Id);
        stored.Value.Select(note => note.RecordedOn).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ItShouldNotDisturbTheTripOrItsCatchNotes()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);

        // Act
        await Sut.UpsertAsync(NewNote(trip.Id, userId), CancellationToken.None);

        // Assert
        var storedTrip = await Trips.GetByIdAsync(trip.Id, CancellationToken.None);
        storedTrip.Value!.Status.Should().Be(TripStatusEnum.Active);
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var catchesForThisAngler = await connection.ExecuteScalarAsync<int>(
            """SELECT COUNT(*) FROM catches WHERE caughtbyuserid = @UserId OR tripid = @TripId;""",
            new { UserId = userId, TripId = trip.Id });
        catchesForThisAngler.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldRemoveOnlyTheDeletedNote()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var kept = NewNote(trip.Id, userId, "kept", recordedOn: StartedOn.AddHours(1));
        var removed = NewNote(trip.Id, userId, "removed", recordedOn: StartedOn.AddHours(2));
        await Sut.UpsertAsync(kept, CancellationToken.None);
        await Sut.UpsertAsync(removed, CancellationToken.None);

        // Act
        var deleted = await Sut.DeleteAsync(removed.Id, CancellationToken.None);

        // Assert
        deleted.IsSuccess.Should().BeTrue();
        var stored = await Sut.GetByTripIdAsync(trip.Id, CancellationToken.None);
        stored.Value.Should().ContainSingle();
        stored.Value[0].Id.Should().Be(kept.Id);
    }

    [Fact]
    public async Task ItShouldRoundTripTheAuthor()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);

        // Act
        var saved = await Sut.UpsertAsync(NewNote(trip.Id, userId), CancellationToken.None);

        // Assert
        saved.Value.CreatedByUserId.Should().Be(userId);
        var reloaded = await Sut.GetByIdAsync(saved.Value.Id, CancellationToken.None);
        reloaded.Value!.CreatedByUserId.Should().Be(userId);
        var listed = await Sut.GetByTripIdAsync(trip.Id, CancellationToken.None);
        listed.Value[0].CreatedByUserId.Should().Be(userId);
    }

    [Fact]
    public async Task ItShouldNotLetAReplayRewriteTheAuthor()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var note = NewNote(trip.Id, userId, "water dropped about a foot");
        await Sut.UpsertAsync(note, CancellationToken.None);

        // Act
        var replay = await Sut.UpsertAsync(
            NewNote(trip.Id, otherUserId, "water dropped about two feet", noteId: note.Id),
            CancellationToken.None);

        // Assert
        replay.Value.CreatedByUserId.Should().Be(userId);
        replay.Value.Text.Should().Be("water dropped about two feet");
        var stored = await Sut.GetByIdAsync(note.Id, CancellationToken.None);
        stored.Value!.CreatedByUserId.Should().Be(userId);
        stored.Value.CreatedByUserId.Should().NotBe(otherUserId);
    }

    [Fact]
    public async Task ItShouldRejectANoteWhoseAuthorIsNotAKnownUser()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);

        // Act
        var saved = await Sut.UpsertAsync(
            NewNote(trip.Id, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        saved.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotReturnAnotherTripsNotes()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var mine = await CreateTripAsync(ownerUserId);
        var theirs = await CreateTripAsync(otherUserId);
        await Sut.UpsertAsync(NewNote(mine.Id, ownerUserId, "mine"), CancellationToken.None);
        var theirNote = NewNote(theirs.Id, otherUserId, "theirs");
        await Sut.UpsertAsync(theirNote, CancellationToken.None);

        // Act
        var stored = await Sut.GetByTripIdAsync(mine.Id, CancellationToken.None);

        // Assert
        stored.Value.Should().ContainSingle();
        stored.Value[0].Text.Should().Be("mine");
        stored.Value.Should().NotContain(note => note.Id == theirNote.Id);
    }

    [Fact]
    public async Task ItShouldSurviveTheServerReconcilingTwoActiveTrips()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var earlier = await CreateTripAsync(userId);
        var note = NewNote(earlier.Id, userId);
        await Sut.UpsertAsync(note, CancellationToken.None);

        // Act
        await Trips.UpsertAsync(
            new Trip
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Status = TripStatusEnum.Active,
                StartedOn = StartedOn.AddHours(2)
            },
            CancellationToken.None);

        // Assert
        var reconciled = await Trips.GetByIdAsync(earlier.Id, CancellationToken.None);
        reconciled.Value!.Status.Should().Be(TripStatusEnum.Completed);
        var stored = await Sut.GetByTripIdAsync(earlier.Id, CancellationToken.None);
        stored.Value.Should().ContainSingle();
        stored.Value[0].Id.Should().Be(note.Id);
    }
}
