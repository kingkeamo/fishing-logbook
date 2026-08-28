using AwesomeAssertions;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.TripNoteWriteServiceTests;

public class WhenTestingRemove : BaseTripNoteWriteServiceTest
{
    [Fact]
    public async Task ItShouldDeleteALocalOnlyNoteWithoutCallingTheServer()
    {
        // Arrange
        var removal = new TripNoteRemovalModel(TripId, OwnerUserId, NoteId, SyncStatus.SavedLocally);

        // Act
        await Sut.RemoveAsync(removal, TripNoteStorageEnum.LocalFirst, CancellationToken.None);

        // Assert
        await MockNoteStore.Received(1).DeleteAsync(
            OwnerUserId,
            TripId,
            NoteId,
            Arg.Any<CancellationToken>());
        await MockTripClient.DidNotReceive().DeleteNoteAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeleteASynchronisedLocalNoteOnTheServerAndOnTheDevice()
    {
        // Arrange
        var removal = new TripNoteRemovalModel(TripId, OwnerUserId, NoteId, SyncStatus.Synchronised);

        // Act
        await Sut.RemoveAsync(removal, TripNoteStorageEnum.LocalFirst, CancellationToken.None);

        // Assert
        await MockTripClient.Received(1).DeleteNoteAsync(
            TripId,
            NoteId,
            Arg.Any<CancellationToken>());
        await MockNoteStore.Received(1).DeleteAsync(
            OwnerUserId,
            TripId,
            NoteId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeleteAHistoricalNoteOnTheServerOnly()
    {
        // Arrange
        var removal = new TripNoteRemovalModel(TripId, OwnerUserId, NoteId);

        // Act
        await Sut.RemoveAsync(removal, TripNoteStorageEnum.Server, CancellationToken.None);

        // Assert
        await MockTripClient.Received(1).DeleteNoteAsync(
            TripId,
            NoteId,
            Arg.Any<CancellationToken>());
        await MockNoteStore.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotDeleteLocallyWhenTheHistoricalDeleteFails()
    {
        // Arrange
        MockTripClient.DeleteNoteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new HttpRequestException("offline"));
        var removal = new TripNoteRemovalModel(TripId, OwnerUserId, NoteId);

        // Act
        var remove = async () =>
            await Sut.RemoveAsync(removal, TripNoteStorageEnum.Server, CancellationToken.None);

        // Assert
        await remove.Should().ThrowAsync<HttpRequestException>();
        await MockNoteStore.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }
}
