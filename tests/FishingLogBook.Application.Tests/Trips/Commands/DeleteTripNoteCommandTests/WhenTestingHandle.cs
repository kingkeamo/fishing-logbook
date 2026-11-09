using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Application.Trips.Contracts.Services;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Commands.DeleteTripNoteCommandTests;

public class WhenTestingHandle
{
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SecondId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly ITripNoteService _mockService = Substitute.For<ITripNoteService>();
    private readonly DeleteTripNoteHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new DeleteTripNoteHandler(_mockService, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.DeleteAsync(Arg.Any<DeleteTripNoteArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail("The service failed."));

        // Act
        var response = await _sut.Handle(
            new DeleteTripNoteCommand { TripId = TripId, NoteId = SecondId },
            CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        await _mockService.Received(1).DeleteAsync(
            Arg.Is<DeleteTripNoteArgs>(args => args.TripId == TripId && args.NoteId == SecondId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPassTheCommandToTheServiceAndReturnSuccess()
    {
        // Arrange
        _mockService.DeleteAsync(Arg.Any<DeleteTripNoteArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var response = await _sut.Handle(
            new DeleteTripNoteCommand { TripId = TripId, NoteId = SecondId },
            CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        await _mockService.Received(1).DeleteAsync(
            Arg.Is<DeleteTripNoteArgs>(args => args.TripId == TripId && args.NoteId == SecondId),
            Arg.Any<CancellationToken>());
    }
}
