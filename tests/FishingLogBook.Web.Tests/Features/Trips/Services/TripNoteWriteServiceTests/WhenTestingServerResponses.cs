using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.TripNoteWriteServiceTests;

public class WhenTestingServerResponses : BaseTripNoteWriteServiceTest
{
    private static TripNoteModel Note()
    {
        return new TripNoteModel(
            NoteId,
            TripId,
            OwnerUserId,
            "water dropped about a foot",
            RecordedOn,
            SyncStatus.Synchronised);
    }

    [Fact]
    public async Task ItShouldKeepTheDraftValuesWhenTheServerReturnsNoNoteForANewHistoricalNote()
    {
        // Arrange
        MockTripClient.RecordNoteAsync(Arg.Any<Guid>(), Arg.Any<RecordTripNoteDto>(), Arg.Any<CancellationToken>())
            .Returns((TripNoteDto?)null);
        var draft = new TripNoteDraftModel(TripId, OwnerUserId, "fish rising", RecordedOn);

        // Act
        var note = await Sut.AddAsync(draft, TripStorageEnum.Server, CancellationToken.None);

        // Assert
        note.Text.Should().Be("fish rising");
        note.RecordedOn.Should().Be(RecordedOn);
        note.Id.Should().NotBeEmpty();
        note.SyncStatus.Should().Be(SyncStatus.Synchronised);
        await MockTripClient.Received(1).RecordNoteAsync(
            TripId,
            Arg.Is<RecordTripNoteDto>(request => request.Text == "fish rising" && request.NoteId == note.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTakeTheTextAndTimeTheServerRecordedForANewHistoricalNote()
    {
        // Arrange
        var recordedByServer = RecordedOn.AddMinutes(3);
        MockTripClient.RecordNoteAsync(Arg.Any<Guid>(), Arg.Any<RecordTripNoteDto>(), Arg.Any<CancellationToken>())
            .Returns(call => new TripNoteDto(
                call.ArgAt<RecordTripNoteDto>(1).NoteId,
                TripId,
                "fish rising (normalised)",
                recordedByServer)
            {
                CreatedByUserId = OwnerUserId
            });
        var draft = new TripNoteDraftModel(TripId, OwnerUserId, "fish rising", RecordedOn);

        // Act
        var note = await Sut.AddAsync(draft, TripStorageEnum.Server, CancellationToken.None);

        // Assert
        note.Text.Should().Be("fish rising (normalised)");
        note.RecordedOn.Should().Be(recordedByServer);
    }

    [Fact]
    public async Task ItShouldKeepTheEditedValuesWhenTheServerReturnsNoNoteForAnEditedHistoricalNote()
    {
        // Arrange
        MockTripClient.RecordNoteAsync(Arg.Any<Guid>(), Arg.Any<RecordTripNoteDto>(), Arg.Any<CancellationToken>())
            .Returns((TripNoteDto?)null);
        var edited = Note() with { Text = "water dropped two feet", RecordedOn = RecordedOn.AddMinutes(-10) };

        // Act
        var updated = await Sut.UpdateAsync(edited, TripStorageEnum.Server, CancellationToken.None);

        // Assert
        updated.Text.Should().Be("water dropped two feet");
        updated.RecordedOn.Should().Be(RecordedOn.AddMinutes(-10));
        updated.SyncStatus.Should().Be(SyncStatus.Synchronised);
        updated.SyncedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldTakeTheTextAndTimeTheServerRecordedForAnEditedHistoricalNote()
    {
        // Arrange
        var recordedByServer = RecordedOn.AddMinutes(7);
        MockTripClient.RecordNoteAsync(Arg.Any<Guid>(), Arg.Any<RecordTripNoteDto>(), Arg.Any<CancellationToken>())
            .Returns(new TripNoteDto(NoteId, TripId, "server text", recordedByServer) { CreatedByUserId = OwnerUserId });

        // Act
        var updated = await Sut.UpdateAsync(Note(), TripStorageEnum.Server, CancellationToken.None);

        // Assert
        updated.Text.Should().Be("server text");
        updated.RecordedOn.Should().Be(recordedByServer);
    }

    [Theory]
    [InlineData(SyncStatus.FailedToSynchronise)]
    [InlineData(SyncStatus.WaitingToSynchronise)]
    [InlineData(SyncStatus.SavedLocally)]
    public async Task ItShouldLeaveAnUnsynchronisedLocalNoteStatusAloneWhenItIsEdited(SyncStatus status)
    {
        // Arrange
        var note = Note() with { SyncStatus = status };

        // Act
        var updated = await Sut.UpdateAsync(note, TripStorageEnum.LocalFirst, CancellationToken.None);

        // Assert
        updated.SyncStatus.Should().Be(status);
        await MockNoteStore.Received(1).SaveAsync(
            Arg.Is<TripNoteModel>(saved => saved.Id == NoteId && saved.SyncStatus == status),
            Arg.Any<CancellationToken>());
        await MockTripClient.DidNotReceive().RecordNoteAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordTripNoteDto>(),
            Arg.Any<CancellationToken>());
    }
}
