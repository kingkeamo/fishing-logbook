using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripRemovalServiceTests;

public sealed class WhenTestingRemoveNote : BaseTripRemovalServiceTest
{
    private static readonly Guid NoteId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task ItShouldDeleteTheNoteWithTheGivenId()
    {
        // Arrange
        var other = Note(Guid.NewGuid(), ViewerId, "Other");
        var note = Note(NoteId, ViewerId);
        var trip = Trip(notes: [other, note]);
        // Act
        var removed = await Sut.RemoveNoteAsync(trip, NoteId, CancellationToken.None);
        // Assert
        removed.Should().BeTrue();
        await MockTrips.Received(1).SaveNoteAsync(trip, note, true, Arg.Any<CancellationToken>());
        await MockTrips.DidNotReceive().SaveNoteAsync(Arg.Any<LocalTripRecordModel>(), other, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotSaveAnythingWhenTheNoteIsNoLongerOnTheTrip()
    {
        // Arrange
        var trip = Trip(notes: [Note(Guid.NewGuid(), ViewerId)]);
        // Act
        var removed = await Sut.RemoveNoteAsync(trip, NoteId, CancellationToken.None);
        // Assert
        removed.Should().BeFalse();
        await MockTrips.DidNotReceive().SaveNoteAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripNoteDto>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportFailureWhenTheNoteCannotBeSaved()
    {
        // Arrange
        MockTrips.SaveNoteAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripNoteDto>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException());
        // Act
        var removed = await Sut.RemoveNoteAsync(Trip(notes: [Note(NoteId, ViewerId)]), NoteId, CancellationToken.None);
        // Assert
        removed.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotHideACancellation()
    {
        // Arrange
        MockTrips.SaveNoteAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripNoteDto>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        // Act
        var act = () => Sut.RemoveNoteAsync(Trip(notes: [Note(NoteId, ViewerId)]), NoteId, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
