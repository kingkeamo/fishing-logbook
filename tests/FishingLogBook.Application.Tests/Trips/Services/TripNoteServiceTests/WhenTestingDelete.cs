using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Trips;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripNoteServiceTests;

public class WhenTestingDelete : BaseTripNoteServiceTest
{
    [Fact]
    public async Task ItShouldFailWhenTheTripIsUnknown()
    {
        // Arrange
        GivenNoTrip();

        // Act
        var result = await Sut.DeleteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockTripNoteRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheTripBelongsToAnotherAngler()
    {
        // Arrange
        GivenTrip(OtherUserId);
        MockTripNoteRepository.GetByIdAsync(NoteId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(StoredNote(TripId)));

        // Act
        var result = await Sut.DeleteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockTripNoteRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheNoteIsUnknown()
    {
        // Arrange
        GivenTrip(CurrentUserId);

        // Act
        var result = await Sut.DeleteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNoteNotFoundError>();
        await MockTripNoteRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheNoteBelongsToAnotherTrip()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        MockTripNoteRepository.GetByIdAsync(NoteId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(StoredNote(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"))));

        // Act
        var result = await Sut.DeleteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNoteNotFoundError>();
        await MockTripNoteRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeleteTheAnglersOwnNote()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        MockTripNoteRepository.GetByIdAsync(NoteId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(StoredNote(TripId)));

        // Act
        var result = await Sut.DeleteAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripNoteRepository.Received(1).DeleteAsync(
            NoteId,
            Arg.Any<CancellationToken>());
    }

    private static DeleteTripNoteArgs Args()
    {
        return new DeleteTripNoteArgs
        {
            TripId = TripId,
            NoteId = NoteId
        };
    }
}
