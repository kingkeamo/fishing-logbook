using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Commands.RemoveTripParticipantCommandTests;

public class WhenTestingHandle
{
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SecondId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly ITripParticipantService _mockService = Substitute.For<ITripParticipantService>();
    private readonly RemoveTripParticipantHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new RemoveTripParticipantHandler(_mockService, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.RemoveAsync(Arg.Any<RemoveTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripParticipantsDto>("The service failed."));

        // Act
        var response = await _sut.Handle(
            new RemoveTripParticipantCommand { TripId = TripId, ParticipantUserId = SecondId },
            CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Participants.Should().BeNull();
        await _mockService.Received(1).RemoveAsync(
            Arg.Is<RemoveTripParticipantArgs>(args => args.TripId == TripId && args.ParticipantUserId == SecondId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPassTheCommandToTheServiceAndReturnSuccess()
    {
        // Arrange
        var payload = new TripParticipantsDto(TripId, "Owner");
        _mockService.RemoveAsync(Arg.Any<RemoveTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(payload));

        // Act
        var response = await _sut.Handle(
            new RemoveTripParticipantCommand { TripId = TripId, ParticipantUserId = SecondId },
            CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Participants.Should().Be(payload);
        await _mockService.Received(1).RemoveAsync(
            Arg.Is<RemoveTripParticipantArgs>(args => args.TripId == TripId && args.ParticipantUserId == SecondId),
            Arg.Any<CancellationToken>());
    }
}
