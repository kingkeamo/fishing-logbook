using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Trips;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripNoteServiceTests;

public class WhenTestingSharedTripNotes : BaseTripNoteServiceTest
{
    [Fact]
    public async Task ItShouldRefuseANoteFromAnAnglerWhoIsNotOnTheSharedTrip()
    {
        // Arrange
        GivenNoTrip();

        // Act
        var result = await Sut.RecordAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockTripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseEditingAnotherAnglersNote()
    {
        // Arrange
        GivenSharedTrip();
        MockTripNoteRepository.GetByIdAsync(NoteId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(StoredNote(TripId, OtherUserId)));

        // Act
        var result = await Sut.RecordAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripContributionNotOwnedError>();
        await MockTripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseDeletingAnotherAnglersNote()
    {
        // Arrange
        GivenSharedTrip();
        MockTripNoteRepository.GetByIdAsync(NoteId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(StoredNote(TripId, OtherUserId)));

        // Act
        var result = await Sut.DeleteAsync(
            new DeleteTripNoteArgs { TripId = TripId, NoteId = NoteId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripContributionNotOwnedError>();
        await MockTripNoteRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseAnEditByTheTripOwnerOfAnotherParticipantsNote()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        MockTripNoteRepository.GetByIdAsync(NoteId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(StoredNote(TripId, OtherUserId)));

        // Act
        var result = await Sut.RecordAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripContributionNotOwnedError>();
        await MockTripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseADeleteByTheTripOwnerOfAnotherParticipantsNote()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        MockTripNoteRepository.GetByIdAsync(NoteId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(StoredNote(TripId, OtherUserId)));

        // Act
        var result = await Sut.DeleteAsync(
            new DeleteTripNoteArgs { TripId = TripId, NoteId = NoteId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripContributionNotOwnedError>();
        await MockTripNoteRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLetAParticipantDeleteTheirOwnNote()
    {
        // Arrange
        GivenSharedTrip();
        MockTripNoteRepository.GetByIdAsync(NoteId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(StoredNote(TripId, CurrentUserId)));

        // Act
        var result = await Sut.DeleteAsync(
            new DeleteTripNoteArgs { TripId = TripId, NoteId = NoteId },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripNoteRepository.Received(1).DeleteAsync(NoteId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecordAParticipantNoteAgainstTheSharedTripWithTheirOwnAuthorship()
    {
        // Arrange
        GivenSharedTrip();

        // Act
        var result = await Sut.RecordAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TripId.Should().Be(TripId);
        result.Value.CreatedByUserId.Should().Be(CurrentUserId);
        result.Value.CreatedByUserId.Should().NotBe(OtherUserId);
        await MockTripNoteRepository.Received(1).UpsertAsync(
            Arg.Is<TripNote>(note =>
                note.TripId == TripId
                && note.CreatedByUserId == CurrentUserId),
            Arg.Any<CancellationToken>());
    }

    private static RecordTripNoteArgs Args()
    {
        return new RecordTripNoteArgs
        {
            TripId = TripId,
            NoteId = NoteId,
            Text = "fish moving on the shallows",
            RecordedOn = RecordedOn
        };
    }
}
