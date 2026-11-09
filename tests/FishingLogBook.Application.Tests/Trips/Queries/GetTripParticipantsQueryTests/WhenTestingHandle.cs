using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Application.Trips.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Queries.GetTripParticipantsQueryTests;

public class WhenTestingHandle
{
    private static readonly Guid Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly ITripParticipantService _mockService = Substitute.For<ITripParticipantService>();
    private readonly GetTripParticipantsHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new GetTripParticipantsHandler(_mockService, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.GetAsync(Arg.Any<GetTripParticipantsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripParticipantsDto>("The service failed."));

        // Act
        var response = await _sut.Handle(new GetTripParticipantsQuery { TripId = Id }, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Participants.Should().BeNull();
        await _mockService.Received(1).GetAsync(
            Arg.Is<GetTripParticipantsArgs>(args => args.TripId == Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheServiceResult()
    {
        // Arrange
        var payload = new TripParticipantsDto(Id, "Owner");
        _mockService.GetAsync(Arg.Any<GetTripParticipantsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(payload));

        // Act
        var response = await _sut.Handle(new GetTripParticipantsQuery { TripId = Id }, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Participants.Should().Be(payload);
        await _mockService.Received(1).GetAsync(
            Arg.Is<GetTripParticipantsArgs>(args => args.TripId == Id),
            Arg.Any<CancellationToken>());
    }
}
