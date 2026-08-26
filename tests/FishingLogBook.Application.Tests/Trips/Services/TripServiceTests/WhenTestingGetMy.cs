using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripServiceTests;

public class WhenTestingGetMy : BaseTripServiceTest
{
    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenTheAnglerHasNoTrips()
    {
        // Arrange
        MockTripRepository.GetByOwnerUserIdAsync(CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Trip>>([]));

        // Act
        var result = await Sut.GetMyAsync(
            new GetMyTripsArgs { UserId = CurrentUserId },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRequestOnlyTheGivenOwnersTrips()
    {
        // Arrange
        MockTripRepository.GetByOwnerUserIdAsync(CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Trip>>([StoredTrip()]));

        // Act
        var result = await Sut.GetMyAsync(
            new GetMyTripsArgs { UserId = CurrentUserId },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].OwnerUserId.Should().Be(CurrentUserId);
        await MockTripRepository.Received(1).GetByOwnerUserIdAsync(
            CurrentUserId,
            Arg.Any<CancellationToken>());
        await MockTripRepository.DidNotReceive().GetByOwnerUserIdAsync(
            OtherUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMapEveryTripIncludingCompletedOnes()
    {
        // Arrange
        var completedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        MockTripRepository.GetByOwnerUserIdAsync(CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Trip>>(
            [
                StoredTrip(),
                StoredTrip(
                    tripId: completedId,
                    status: TripStatusEnum.Completed,
                    endedOn: StartedOn.AddHours(4))
            ]));

        // Act
        var result = await Sut.GetMyAsync(
            new GetMyTripsArgs { UserId = CurrentUserId },
            CancellationToken.None);

        // Assert
        result.Value.Should().HaveCount(2);
        result.Value.Select(trip => trip.Id).Should().Contain([TripId, completedId]);
    }

    [Fact]
    public async Task ItShouldReturnTheRepositoryFailure()
    {
        // Arrange
        MockTripRepository.GetByOwnerUserIdAsync(CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<Trip>>("Failed to save the trip."));

        // Act
        var result = await Sut.GetMyAsync(
            new GetMyTripsArgs { UserId = CurrentUserId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
    }
}
