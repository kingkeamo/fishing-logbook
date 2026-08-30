using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripAccessServiceTests;

public class WhenTestingRequireOwner : BaseTripAccessServiceTest
{
    [Fact]
    public async Task ItShouldReportNotFoundWhenTheTripDoesNotExist()
    {
        // Arrange
        GivenNoTrip();

        // Act
        var result = await Sut.RequireOwnerAsync(TripId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockTripParticipantRepository.DidNotReceive().FindAsync(
            Arg.Any<FindTripParticipantArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportNotFoundForAnAnglerWhoCannotSeeTheTrip()
    {
        // Arrange
        GivenTrip(OwnerUserId);

        // Act
        var result = await Sut.RequireOwnerAsync(TripId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
    }

    [Fact]
    public async Task ItShouldTellAnAcceptedParticipantTheActionIsOwnerOnly()
    {
        // Arrange
        GivenTrip(OwnerUserId);
        GivenParticipant();

        // Act
        var result = await Sut.RequireOwnerAsync(TripId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripOwnerActionRequiredError>();
        await MockTripParticipantRepository.Received(1).FindAsync(
            Arg.Is<FindTripParticipantArgs>(args =>
                args.TripId == TripId && args.UserId == CurrentUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowTheOwner()
    {
        // Arrange
        GivenTrip(CurrentUserId);

        // Act
        var result = await Sut.RequireOwnerAsync(TripId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(TripAccessRoleEnum.Owner);
        result.Value.Trip.OwnerUserId.Should().Be(CurrentUserId);
        await MockTripRepository.Received(1).GetByIdAsync(TripId, Arg.Any<CancellationToken>());
    }
}
