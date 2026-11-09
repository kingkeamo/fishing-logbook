using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Commands.InviteTripParticipantCommandTests;

public class WhenTestingHandle
{
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SecondId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly ITripParticipantService _mockService = Substitute.For<ITripParticipantService>();
    private readonly InviteTripParticipantHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new InviteTripParticipantHandler(_mockService, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.InviteAsync(Arg.Any<InviteTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripParticipantsDto>("The service failed."));

        // Act
        var response = await _sut.Handle(
            new InviteTripParticipantCommand { TripId = TripId, InvitedUserId = SecondId },
            CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Participants.Should().BeNull();
        await _mockService.Received(1).InviteAsync(
            Arg.Is<InviteTripParticipantArgs>(args => args.TripId == TripId && args.InvitedUserId == SecondId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPassTheCommandToTheServiceAndReturnSuccess()
    {
        // Arrange
        var payload = new TripParticipantsDto(TripId, "Owner");
        _mockService.InviteAsync(Arg.Any<InviteTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(payload));

        // Act
        var response = await _sut.Handle(
            new InviteTripParticipantCommand { TripId = TripId, InvitedUserId = SecondId },
            CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Participants.Should().Be(payload);
        await _mockService.Received(1).InviteAsync(
            Arg.Is<InviteTripParticipantArgs>(args => args.TripId == TripId && args.InvitedUserId == SecondId),
            Arg.Any<CancellationToken>());
    }
}
