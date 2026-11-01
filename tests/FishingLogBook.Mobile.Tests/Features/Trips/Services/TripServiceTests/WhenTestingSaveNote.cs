using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;

public sealed class WhenTestingSaveNote : BaseTripServiceTest
{
    private async Task<LocalTripRecordModel> StartOfflineAsync()
    {
        MockConnectivity.IsOnline.Returns(false);
        return await TripSut.StartAsync("Corrib", null, CancellationToken.None);
    }

    private static TripNoteDto Note(LocalTripRecordModel trip, string text = "Fish rising", DateTimeOffset? at = null, Guid? by = null, Guid? id = null)
    {
        return new TripNoteDto(id ?? Guid.NewGuid(), trip.Detail.Trip.Id, text, at ?? trip.Detail.Trip.StartedOn) { CreatedByUserId = by ?? OwnerUserId };
    }

    [Fact]
    public async Task ItShouldRefuseANoteFromSomeoneWhoCannotContributeToTheTrip()
    {
        // Arrange
        var trip = (await StartOfflineAsync()) with { AccessRevoked = true };
        // Act
        var act = () => TripSut.SaveNoteAsync(trip, Note(trip), deleted: false, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldRefuseANoteWrittenByAnotherAnglerOrForAnotherTrip()
    {
        // Arrange
        var trip = await StartOfflineAsync();
        // Act
        var byAnother = () => TripSut.SaveNoteAsync(trip, Note(trip, by: OtherUserId), deleted: false, CancellationToken.None);
        var forAnother = () => TripSut.SaveNoteAsync(trip, Note(trip) with { TripId = Guid.NewGuid() }, deleted: false, CancellationToken.None);
        // Assert
        await byAnother.Should().ThrowAsync<UnauthorizedAccessException>();
        await forAnother.Should().ThrowAsync<UnauthorizedAccessException>();
        (await TripStore.ListContributionsAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRejectABlankNoteOrOneOutsideTheTripWindow()
    {
        // Arrange
        var trip = await StartOfflineAsync();
        // Act
        var blank = () => TripSut.SaveNoteAsync(trip, Note(trip, text: "   "), deleted: false, CancellationToken.None);
        var beforeStart = () => TripSut.SaveNoteAsync(trip, Note(trip, at: trip.Detail.Trip.StartedOn.AddTicks(-1)), deleted: false, CancellationToken.None);
        var inTheFuture = () => TripSut.SaveNoteAsync(trip, Note(trip, at: Now.AddTicks(1)), deleted: false, CancellationToken.None);
        // Assert
        await blank.Should().ThrowAsync<ArgumentException>();
        await beforeStart.Should().ThrowAsync<ArgumentException>();
        await inTheFuture.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ItShouldAcceptANoteAtTheExactStartAndNow()
    {
        // Arrange
        var trip = await StartOfflineAsync();
        // Act
        await TripSut.SaveNoteAsync(trip, Note(trip, at: trip.Detail.Trip.StartedOn), deleted: false, CancellationToken.None);
        await TripSut.SaveNoteAsync(trip, Note(trip, at: Now), deleted: false, CancellationToken.None);
        // Assert
        (await TripSut.GetAsync(trip.Detail.Trip.Id, CancellationToken.None))!.Detail.Notes.Should().HaveCount(2);
    }

    [Fact]
    public async Task ItShouldTrimTheNoteStoreItPendingAndShowItOnTheTripAcrossARestart()
    {
        // Arrange
        var trip = await StartOfflineAsync();
        var note = Note(trip, "  Water dropped  ");
        // Act
        await TripSut.SaveNoteAsync(trip, note, deleted: false, CancellationToken.None);
        // Assert
        var reopened = await CreateTripStore().ListContributionsAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None);
        reopened.Should().ContainSingle(item => item.Id == note.Id && item.IsPending && item.Note!.Text == "Water dropped" && !item.Deleted);
        (await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None))
            .Should().Contain(operation => operation.AggregateId == note.Id && operation.OperationType == OutboxOperationTypeEnum.WriteTripNote);
        (await TripSut.GetAsync(trip.Detail.Trip.Id, CancellationToken.None))!.Detail.Notes.Should().ContainSingle(item => item.Text == "Water dropped");
    }

    [Fact]
    public async Task ItShouldReplaceTheTextOfAnExistingNoteWithoutDuplicatingIt()
    {
        // Arrange
        var trip = await StartOfflineAsync();
        var note = Note(trip, "First");
        await TripSut.SaveNoteAsync(trip, note, deleted: false, CancellationToken.None);
        // Act
        await TripSut.SaveNoteAsync(trip, note with { Text = "Second" }, deleted: false, CancellationToken.None);
        // Assert
        var notes = (await TripSut.GetAsync(trip.Detail.Trip.Id, CancellationToken.None))!.Detail.Notes;
        notes.Should().ContainSingle(item => item.Id == note.Id && item.Text == "Second");
        (await TripStore.ListContributionsAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None)).Should().ContainSingle().Which.Revision.Should().Be(2);
    }

    [Fact]
    public async Task ItShouldRemoveADeletedNoteFromTheTripAndKeepTheDeletionPending()
    {
        // Arrange
        var trip = await StartOfflineAsync();
        var note = Note(trip);
        await TripSut.SaveNoteAsync(trip, note, deleted: false, CancellationToken.None);
        // Act
        await TripSut.SaveNoteAsync(trip, note with { RecordedOn = trip.Detail.Trip.StartedOn.AddYears(-1) }, deleted: true, CancellationToken.None);
        // Assert
        (await TripSut.GetAsync(trip.Detail.Trip.Id, CancellationToken.None))!.Detail.Notes.Should().BeEmpty();
        (await TripStore.ListContributionsAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None))
            .Should().ContainSingle(item => item.Deleted && item.IsPending);
    }
}
