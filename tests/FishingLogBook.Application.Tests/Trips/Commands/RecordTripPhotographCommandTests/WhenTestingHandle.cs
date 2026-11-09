using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Commands.RecordTripPhotographCommandTests;

public class WhenTestingHandle
{
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PhotographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset AddedOn = DateTimeOffset.Parse("2026-09-12T06:30:00Z");
    private static readonly DateTimeOffset CapturedOn = DateTimeOffset.Parse("2026-09-12T05:00:00Z");

    private readonly ITripPhotographService _mockService = Substitute.For<ITripPhotographService>();
    private readonly RecordTripPhotographHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new RecordTripPhotographHandler(_mockService, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.RecordAsync(Arg.Any<RecordTripPhotographArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripPhotographDto>("The service failed."));

        // Act
        var response = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Photograph.Should().BeNull();
        await _mockService.Received(1).RecordAsync(
            Arg.Is<RecordTripPhotographArgs>(args => MatchesCommand(args)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMapTheNestedPhotographAndReturnTheRecordedPhotograph()
    {
        // Arrange
        var photograph = new TripPhotographDto(PhotographId, TripId, "trips/key.jpg", "image/jpeg", AddedOn, CapturedOn);
        _mockService.RecordAsync(Arg.Any<RecordTripPhotographArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(photograph));

        // Act
        var response = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Photograph.Should().Be(photograph);
        await _mockService.Received(1).RecordAsync(
            Arg.Is<RecordTripPhotographArgs>(args => MatchesCommand(args)),
            Arg.Any<CancellationToken>());
    }

    private static RecordTripPhotographCommand Command()
    {
        return new RecordTripPhotographCommand
        {
            TripId = TripId,
            Photograph = new RecordTripPhotographDto(PhotographId, "trips/key.jpg", "image/jpeg", AddedOn, CapturedOn)
        };
    }

    private static bool MatchesCommand(RecordTripPhotographArgs args)
    {
        return args.TripId == TripId
            && args.PhotographId == PhotographId
            && args.ObjectKey == "trips/key.jpg"
            && args.ContentType == "image/jpeg"
            && args.AddedOn == AddedOn
            && args.CapturedOn == CapturedOn;
    }
}
