using AwesomeAssertions;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Application.Trips.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Queries.GetMyTripInvitationsQueryTests;

public class WhenTestingHandle
{
    private readonly ITripParticipantService _mockService = Substitute.For<ITripParticipantService>();
    private readonly GetMyTripInvitationsHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new GetMyTripInvitationsHandler(_mockService);
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.GetMyInvitationsAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<TripInvitationDto>>("The service failed."));

        // Act
        var response = await _sut.Handle(new GetMyTripInvitationsQuery(), CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Invitations.Should().BeEmpty();
        await _mockService.Received(1).GetMyInvitationsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheInvitations()
    {
        // Arrange
        IReadOnlyList<TripInvitationDto> invitations =
        [
            new TripInvitationDto(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                "Sam",
                "Lough trip",
                "Lough Corrib",
                DateTimeOffset.Parse("2026-09-12T06:30:00Z"),
                DateTimeOffset.Parse("2026-09-10T08:00:00Z"))
        ];
        _mockService.GetMyInvitationsAsync(Arg.Any<CancellationToken>()).Returns(Result.Ok(invitations));

        // Act
        var response = await _sut.Handle(new GetMyTripInvitationsQuery(), CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Invitations.Should().Equal(invitations);
        await _mockService.Received(1).GetMyInvitationsAsync(Arg.Any<CancellationToken>());
    }
}
