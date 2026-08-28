using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.TripNoteWriteServiceTests;

public class WhenTestingAdd : BaseTripNoteWriteServiceTest
{
    [Fact]
    public async Task ItShouldSaveALocalTripNoteThroughTheOfflineStore()
    {
        // Arrange
        var draft = new TripNoteDraftModel(TripId, OwnerUserId, "changed to olive nymph", RecordedOn);

        // Act
        var note = await Sut.AddAsync(draft, TripNoteStorageEnum.LocalFirst, CancellationToken.None);

        // Assert
        note.TripId.Should().Be(TripId);
        note.OwnerUserId.Should().Be(OwnerUserId);
        note.Text.Should().Be("changed to olive nymph");
        note.RecordedOn.Should().Be(RecordedOn);
        note.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        await MockNoteStore.Received(1).SaveAsync(
            Arg.Is<TripNoteModel>(saved =>
                saved.TripId == TripId
                && saved.OwnerUserId == OwnerUserId
                && saved.Text == "changed to olive nymph"
                && saved.RecordedOn == RecordedOn
                && saved.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
        await MockTripClient.DidNotReceive().RecordNoteAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordTripNoteDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPostAHistoricalTripNoteWithoutTouchingTheOfflineStore()
    {
        // Arrange
        var draft = new TripNoteDraftModel(
            TripId,
            OwnerUserId,
            "fish started rising beside the reeds",
            RecordedOn);

        // Act
        var note = await Sut.AddAsync(draft, TripNoteStorageEnum.Server, CancellationToken.None);

        // Assert
        note.RecordedOn.Should().Be(RecordedOn);
        note.SyncStatus.Should().Be(SyncStatus.Synchronised);
        await MockTripClient.Received(1).RecordNoteAsync(
            TripId,
            Arg.Is<RecordTripNoteDto>(request =>
                request.Text == "fish started rising beside the reeds"
                && request.RecordedOn == RecordedOn
                && request.NoteId != Guid.Empty),
            Arg.Any<CancellationToken>());
        await MockNoteStore.DidNotReceive().SaveAsync(
            Arg.Any<TripNoteModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTakeTheStoredIdentityFromTheServerResponse()
    {
        // Arrange
        MockTripClient.RecordNoteAsync(
                Arg.Any<Guid>(),
                Arg.Any<RecordTripNoteDto>(),
                Arg.Any<CancellationToken>())
            .Returns(new TripNoteDto(NoteId, TripId, "trimmed by the server", RecordedOn)
            {
                CreatedByUserId = OwnerUserId
            });
        var draft = new TripNoteDraftModel(TripId, OwnerUserId, "  trimmed by the server  ", RecordedOn);

        // Act
        var note = await Sut.AddAsync(draft, TripNoteStorageEnum.Server, CancellationToken.None);

        // Assert
        note.Id.Should().Be(NoteId);
        note.Text.Should().Be("trimmed by the server");
        await MockNoteStore.DidNotReceive().SaveAsync(
            Arg.Any<TripNoteModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotWriteALocalNoteWhenTheHistoricalPostFails()
    {
        // Arrange
        MockTripClient.RecordNoteAsync(
                Arg.Any<Guid>(),
                Arg.Any<RecordTripNoteDto>(),
                Arg.Any<CancellationToken>())
            .Returns<TripNoteDto?>(_ => throw new HttpRequestException("offline"));
        var draft = new TripNoteDraftModel(TripId, OwnerUserId, "wind picked up", RecordedOn);

        // Act
        var add = async () =>
            await Sut.AddAsync(draft, TripNoteStorageEnum.Server, CancellationToken.None);

        // Assert
        await add.Should().ThrowAsync<HttpRequestException>();
        await MockNoteStore.DidNotReceive().SaveAsync(
            Arg.Any<TripNoteModel>(),
            Arg.Any<CancellationToken>());
    }
}
