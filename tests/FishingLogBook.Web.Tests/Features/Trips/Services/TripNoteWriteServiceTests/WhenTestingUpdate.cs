using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.TripNoteWriteServiceTests;

public class WhenTestingUpdate : BaseTripNoteWriteServiceTest
{
    [Fact]
    public async Task ItShouldKeepALocalOnlyNoteWaitingToSynchronise()
    {
        // Arrange
        var note = Note(SyncStatus.SavedLocally) with { Text = "changed to olive nymph" };

        // Act
        var updated = await Sut.UpdateAsync(note, TripStorageEnum.LocalFirst, CancellationToken.None);

        // Assert
        updated.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        await MockNoteStore.Received(1).SaveAsync(
            Arg.Is<TripNoteModel>(saved =>
                saved.Id == NoteId
                && saved.Text == "changed to olive nymph"
                && saved.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
        await MockTripClient.DidNotReceive().RecordNoteAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordTripNoteDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldQueueAnAlreadySynchronisedLocalNoteForResending()
    {
        // Arrange
        var note = Note(SyncStatus.Synchronised) with { RecordedOn = RecordedOn.AddHours(1) };

        // Act
        var updated = await Sut.UpdateAsync(note, TripStorageEnum.LocalFirst, CancellationToken.None);

        // Assert
        updated.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        await MockNoteStore.Received(1).SaveAsync(
            Arg.Is<TripNoteModel>(saved =>
                saved.RecordedOn == RecordedOn.AddHours(1)
                && saved.SyncStatus == SyncStatus.WaitingToSynchronise),
            Arg.Any<CancellationToken>());
        await MockTripClient.DidNotReceive().RecordNoteAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordTripNoteDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSendAHistoricalNoteEditToTheServerOnly()
    {
        // Arrange
        var note = Note(SyncStatus.Synchronised) with
        {
            Text = "fish started rising beside the reeds",
            RecordedOn = RecordedOn.AddMinutes(-45)
        };

        // Act
        var updated = await Sut.UpdateAsync(note, TripStorageEnum.Server, CancellationToken.None);

        // Assert
        updated.SyncStatus.Should().Be(SyncStatus.Synchronised);
        updated.RecordedOn.Should().Be(RecordedOn.AddMinutes(-45));
        await MockTripClient.Received(1).RecordNoteAsync(
            TripId,
            Arg.Is<RecordTripNoteDto>(request =>
                request.NoteId == NoteId
                && request.Text == "fish started rising beside the reeds"
                && request.RecordedOn == RecordedOn.AddMinutes(-45)),
            Arg.Any<CancellationToken>());
        await MockNoteStore.DidNotReceive().SaveAsync(
            Arg.Any<TripNoteModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotWriteLocallyWhenTheHistoricalEditFails()
    {
        // Arrange
        MockTripClient.RecordNoteAsync(
                Arg.Any<Guid>(),
                Arg.Any<RecordTripNoteDto>(),
                Arg.Any<CancellationToken>())
            .Returns<TripNoteDto?>(_ => throw new HttpRequestException("offline"));

        // Act
        var update = async () =>
            await Sut.UpdateAsync(Note(SyncStatus.Synchronised), TripStorageEnum.Server, CancellationToken.None);

        // Assert
        await update.Should().ThrowAsync<HttpRequestException>();
        await MockNoteStore.DidNotReceive().SaveAsync(
            Arg.Any<TripNoteModel>(),
            Arg.Any<CancellationToken>());
    }

    private static TripNoteModel Note(SyncStatus syncStatus)
    {
        return new TripNoteModel(
            NoteId,
            TripId,
            OwnerUserId,
            "water dropped about a foot",
            RecordedOn,
            syncStatus);
    }
}
