using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Commands.RecordTripNoteCommandTests;

public class WhenTestingHandle
{
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid NoteId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset RecordedOn = DateTimeOffset.Parse("2026-09-12T06:30:00Z");

    private readonly ITripNoteService _mockService = Substitute.For<ITripNoteService>();
    private readonly RecordTripNoteHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new RecordTripNoteHandler(_mockService, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.RecordAsync(Arg.Any<RecordTripNoteArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripNoteDto>("The service failed."));

        // Act
        var response = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Note.Should().BeNull();
        await _mockService.Received(1).RecordAsync(
            Arg.Is<RecordTripNoteArgs>(args => MatchesCommand(args)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMapTheNestedNoteAndReturnTheRecordedNote()
    {
        // Arrange
        var note = new TripNoteDto(NoteId, TripId, "Fish rising", RecordedOn);
        _mockService.RecordAsync(Arg.Any<RecordTripNoteArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(note));

        // Act
        var response = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Note.Should().Be(note);
        await _mockService.Received(1).RecordAsync(
            Arg.Is<RecordTripNoteArgs>(args => MatchesCommand(args)),
            Arg.Any<CancellationToken>());
    }

    private static RecordTripNoteCommand Command()
    {
        return new RecordTripNoteCommand
        {
            TripId = TripId,
            Note = new RecordTripNoteDto(NoteId, "Fish rising", RecordedOn)
        };
    }

    private static bool MatchesCommand(RecordTripNoteArgs args)
    {
        return args.TripId == TripId
            && args.NoteId == NoteId
            && args.Text == "Fish rising"
            && args.RecordedOn == RecordedOn;
    }
}
