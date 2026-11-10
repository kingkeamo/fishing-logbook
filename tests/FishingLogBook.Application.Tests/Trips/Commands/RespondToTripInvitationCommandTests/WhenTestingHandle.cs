using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Domain.Enums;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Commands.RespondToTripInvitationCommandTests;

public class WhenTestingHandle
{
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly ITripParticipantService _mockService = Substitute.For<ITripParticipantService>();
    private readonly RespondToTripInvitationHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new RespondToTripInvitationHandler(_mockService, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.RespondAsync(Arg.Any<RespondToTripInvitationArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail("The service failed."));

        // Act
        var response = await _sut.Handle(
            new RespondToTripInvitationCommand { TripId = TripId, Response = TripParticipantStatusEnum.Declined },
            CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        await _mockService.Received(1).RespondAsync(
            Arg.Is<RespondToTripInvitationArgs>(args =>
                args.TripId == TripId && args.Response == TripParticipantStatusEnum.Declined),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPassTheResponseToTheServiceAndReturnSuccess()
    {
        // Arrange
        _mockService.RespondAsync(Arg.Any<RespondToTripInvitationArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var response = await _sut.Handle(
            new RespondToTripInvitationCommand { TripId = TripId, Response = TripParticipantStatusEnum.Accepted },
            CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        await _mockService.Received(1).RespondAsync(
            Arg.Is<RespondToTripInvitationArgs>(args =>
                args.TripId == TripId && args.Response == TripParticipantStatusEnum.Accepted),
            Arg.Any<CancellationToken>());
    }
}
