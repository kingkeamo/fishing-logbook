using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripNoteServiceTests;

public class WhenTestingRecord : BaseTripNoteServiceTest
{
    [Fact]
    public async Task ItShouldRejectAnEmptyNote()
    {
        // Arrange
        GivenTrip(CurrentUserId);

        // Act
        var result = await Sut.RecordAsync(Args(string.Empty), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNoteInvalidError>();
        await MockTripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
            Arg.Any<CancellationToken>());
        await MockTripAccessService.DidNotReceive().RequireContributorAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAWhitespaceOnlyNote()
    {
        // Arrange
        GivenTrip(CurrentUserId);

        // Act
        var result = await Sut.RecordAsync(Args("   \t\r\n  "), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNoteInvalidError>();
        await MockTripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectANoteOneCharacterOverTheCap()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        var text = new string('a', TripConstants.MaxNoteTextLength + 1);

        // Act
        var result = await Sut.RecordAsync(Args(text), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNoteInvalidError>();
        await MockTripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheTripIsUnknown()
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
    public async Task ItShouldFailWhenTheTripBelongsToAnotherAngler()
    {
        // Arrange
        GivenTrip(OtherUserId);

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
    public async Task ItShouldReportTheSameErrorForAnUnknownAndAnotherAnglersTrip()
    {
        // Arrange
        GivenNoTrip();
        var unknown = await Sut.RecordAsync(Args(), CancellationToken.None);
        GivenTrip(OtherUserId);

        // Act
        var foreign = await Sut.RecordAsync(Args(), CancellationToken.None);

        // Assert
        foreign.Errors[0].GetType().Should().Be(unknown.Errors[0].GetType());
        foreign.Errors[0].Message.Should().Be(unknown.Errors[0].Message);
    }

    [Fact]
    public async Task ItShouldRejectANoteAlreadyBelongingToAnotherTrip()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        MockTripNoteRepository.GetByIdAsync(NoteId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(StoredNote(Guid.NewGuid())));

        // Act
        var result = await Sut.RecordAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNoteNotFoundError>();
        await MockTripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTrimSurroundingWhitespace()
    {
        // Arrange
        GivenTrip(CurrentUserId);

        // Act
        var result = await Sut.RecordAsync(
            Args("  fish rising near the reeds \n "),
            CancellationToken.None);

        // Assert
        result.Value.Text.Should().Be("fish rising near the reeds");
        await MockTripNoteRepository.Received(1).UpsertAsync(
            Arg.Is<TripNote>(note => note.Text == "fish rising near the reeds"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAcceptANoteAtExactlyTheCap()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        var text = new string('a', TripConstants.MaxNoteTextLength);

        // Act
        var result = await Sut.RecordAsync(Args(text), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().HaveLength(TripConstants.MaxNoteTextLength);
    }

    [Fact]
    public async Task ItShouldAcceptANoteForACompletedTrip()
    {
        // Arrange
        GivenTrip(CurrentUserId, TripStatusEnum.Completed);

        // Act
        var result = await Sut.RecordAsync(Args("a good day"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripNoteRepository.Received(1).UpsertAsync(
            Arg.Is<TripNote>(note => note.TripId == TripId && note.Text == "a good day"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheClientRecordedInstant()
    {
        // Arrange
        GivenTrip(CurrentUserId);

        // Act
        var result = await Sut.RecordAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(NoteId);
        result.Value.TripId.Should().Be(TripId);
        result.Value.RecordedOn.Should().Be(RecordedOn);
        await MockTripNoteRepository.Received(1).UpsertAsync(
            Arg.Is<TripNote>(note =>
                note.Id == NoteId
                && note.TripId == TripId
                && note.RecordedOn == RecordedOn),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTakeTheAuthorFromTheTrustedCurrentUser()
    {
        // Arrange
        GivenTrip(CurrentUserId);

        // Act
        var result = await Sut.RecordAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedByUserId.Should().Be(CurrentUserId);
        await MockTripNoteRepository.Received(1).UpsertAsync(
            Arg.Is<TripNote>(note => note.CreatedByUserId == CurrentUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ItShouldNotAcceptAnAuthorFromTheRequestPayload()
    {
        // Arrange
        var payloadProperties = typeof(RecordTripNoteDto).GetProperties();

        // Act
        var authorLike = payloadProperties
            .Where(property => property.Name.Contains("User", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Author", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("CreatedBy", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Assert
        authorLike.Should().BeEmpty();
        payloadProperties.Select(property => property.Name)
            .Should().BeEquivalentTo(["NoteId", "Text", "RecordedOn"]);
    }

    [Fact]
    public async Task ItShouldRejectANoteRecordedBeforeTheTripStarted()
    {
        // Arrange
        GivenTrip(CurrentUserId);

        // Act
        var result = await Sut.RecordAsync(
            Args(recordedOn: StartedOn.AddMinutes(-1)),
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNoteOutsideTripError>();
        await MockTripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectANoteRecordedAfterACompletedTripFinished()
    {
        // Arrange
        GivenTrip(CurrentUserId, TripStatusEnum.Completed);

        // Act
        var result = await Sut.RecordAsync(
            Args(recordedOn: StartedOn.AddHours(3).AddMinutes(1)),
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNoteOutsideTripError>();
        await MockTripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectANoteRecordedInTheFutureOnAnActiveTrip()
    {
        // Arrange
        GivenTrip(CurrentUserId);

        // Act
        var result = await Sut.RecordAsync(
            Args(recordedOn: DateTimeOffset.UtcNow.AddHours(1)),
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNoteOutsideTripError>();
        await MockTripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAcceptANoteRecordedExactlyWhenTheTripStarted()
    {
        // Arrange
        GivenTrip(CurrentUserId);

        // Act
        var result = await Sut.RecordAsync(Args(recordedOn: StartedOn), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripNoteRepository.Received(1).UpsertAsync(
            Arg.Is<TripNote>(note => note.RecordedOn == StartedOn),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAcceptANoteRecordedExactlyWhenACompletedTripFinished()
    {
        // Arrange
        var endedOn = StartedOn.AddHours(3);
        GivenTrip(CurrentUserId, TripStatusEnum.Completed);

        // Act
        var result = await Sut.RecordAsync(Args(recordedOn: endedOn), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripNoteRepository.Received(1).UpsertAsync(
            Arg.Is<TripNote>(note => note.RecordedOn == endedOn),
            Arg.Any<CancellationToken>());
    }

    private static RecordTripNoteArgs Args(
        string text = "water dropped about a foot",
        DateTimeOffset? recordedOn = null)
    {
        return new RecordTripNoteArgs
        {
            TripId = TripId,
            NoteId = NoteId,
            Text = text,
            RecordedOn = recordedOn ?? RecordedOn
        };
    }
}
