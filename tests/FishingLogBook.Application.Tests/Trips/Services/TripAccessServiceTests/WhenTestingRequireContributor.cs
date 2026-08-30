using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripAccessServiceTests;

public class WhenTestingRequireContributor : BaseTripAccessServiceTest
{
    [Fact]
    public async Task ItShouldRejectAnUnresolvedCaller()
    {
        // Arrange
        MockCurrentUser.IsResolved.Returns(false);

        // Act
        var result = await Sut.RequireContributorAsync(TripId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CurrentUserUnresolvedError>();
        await MockTripRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheRepositoryFailure()
    {
        // Arrange
        MockTripRepository.GetByIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Trip?>("Failed to save the trip."));

        // Act
        var result = await Sut.RequireContributorAsync(TripId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save the trip.");
        await MockTripParticipantRepository.DidNotReceive().FindAsync(
            Arg.Any<FindTripParticipantArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportNotFoundWhenTheTripDoesNotExist()
    {
        // Arrange
        GivenNoTrip();

        // Act
        var result = await Sut.RequireContributorAsync(TripId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockTripParticipantRepository.DidNotReceive().FindAsync(
            Arg.Any<FindTripParticipantArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportNotFoundForAnAnglerWithNoInvitation()
    {
        // Arrange
        GivenTrip(OwnerUserId);

        // Act
        var result = await Sut.RequireContributorAsync(TripId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockTripParticipantRepository.Received(1).FindAsync(
            Arg.Is<FindTripParticipantArgs>(args =>
                args.TripId == TripId && args.UserId == CurrentUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportNotFoundForAPendingInvitation()
    {
        // Arrange
        GivenTrip(OwnerUserId);
        GivenParticipant(TripParticipantStatusEnum.Pending);

        // Act
        var result = await Sut.RequireContributorAsync(TripId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
    }

    [Fact]
    public async Task ItShouldReportNotFoundForADeclinedInvitation()
    {
        // Arrange
        GivenTrip(OwnerUserId);
        GivenParticipant(TripParticipantStatusEnum.Declined);

        // Act
        var result = await Sut.RequireContributorAsync(TripId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
    }

    [Fact]
    public async Task ItShouldReportNotFoundForARemovedParticipant()
    {
        // Arrange
        GivenTrip(OwnerUserId);
        GivenParticipant(removedOn: StartedOn.AddHours(2));

        // Act
        var result = await Sut.RequireContributorAsync(TripId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
    }

    [Fact]
    public async Task ItShouldReportTheSameErrorForAnUnknownTripAndOneTheAnglerCannotSee()
    {
        // Arrange
        GivenNoTrip();
        var unknown = await Sut.RequireContributorAsync(TripId, CancellationToken.None);
        GivenTrip(OwnerUserId);

        // Act
        var foreign = await Sut.RequireContributorAsync(TripId, CancellationToken.None);

        // Assert
        foreign.Errors[0].GetType().Should().Be(unknown.Errors[0].GetType());
        foreign.Errors[0].Message.Should().Be(unknown.Errors[0].Message);
    }

    [Fact]
    public async Task ItShouldAllowAnAcceptedParticipant()
    {
        // Arrange
        GivenTrip(OwnerUserId);
        GivenParticipant();

        // Act
        var result = await Sut.RequireContributorAsync(TripId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(TripAccessRoleEnum.Participant);
        result.Value.Trip.Id.Should().Be(TripId);
        result.Value.Trip.OwnerUserId.Should().Be(OwnerUserId);
        result.Value.CanContribute.Should().BeTrue();
        result.Value.CanManageTrip.Should().BeFalse();
        await MockTripRepository.Received(1).GetByIdAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowTheOwnerWithoutAParticipantLookup()
    {
        // Arrange
        GivenTrip(CurrentUserId);

        // Act
        var result = await Sut.RequireContributorAsync(TripId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(TripAccessRoleEnum.Owner);
        result.Value.CanManageTrip.Should().BeTrue();
        await MockTripRepository.Received(1).GetByIdAsync(TripId, Arg.Any<CancellationToken>());
        await MockTripParticipantRepository.DidNotReceive().FindAsync(
            Arg.Any<FindTripParticipantArgs>(),
            Arg.Any<CancellationToken>());
    }
}
