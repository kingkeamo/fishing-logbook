using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Trips;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripNoteServiceTests;

public class WhenTestingDependencyFailures : BaseTripNoteServiceTest
{
    [Fact]
    public async Task ItShouldReturnTheNoteLookupFailureWhenRecording()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        MockTripNoteRepository.GetByIdAsync(NoteId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripNote?>("The note lookup failed."));

        // Act
        var result = await Sut.RecordAsync(
            new RecordTripNoteArgs { TripId = TripId, NoteId = NoteId, Text = "Fish rising", RecordedOn = RecordedOn },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The note lookup failed.");
        await MockTripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheNoteLookupFailureWhenDeleting()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        MockTripNoteRepository.GetByIdAsync(NoteId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripNote?>("The note lookup failed."));

        // Act
        var result = await Sut.DeleteAsync(
            new DeleteTripNoteArgs { TripId = TripId, NoteId = NoteId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The note lookup failed.");
        await MockTripNoteRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }
}
