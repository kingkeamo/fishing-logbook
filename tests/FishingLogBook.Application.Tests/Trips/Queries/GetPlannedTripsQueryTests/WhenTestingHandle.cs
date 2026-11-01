using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Queries.GetPlannedTripsQueryTests;

public class WhenTestingHandle : BaseGetPlannedTripsQueryTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        MockPlannedTripService
            .GetEligibleAsync(Arg.Any<GetPlannedTripsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<PlannedTripOptionDto>>("Failed to save the trip."));

        // Act
        var response = await Sut.Handle(new GetPlannedTripsQuery { UserId = UserId }, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.Trips.Should().BeEmpty();
        await MockPlannedTripService.Received(1).GetEligibleAsync(
            Arg.Is<GetPlannedTripsArgs>(args => args.UserId == UserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPassTheUserAndWindowToTheServiceAndReturnItsTrips()
    {
        // Arrange
        var option = new PlannedTripOptionDto(Guid.NewGuid(), DateTimeOffset.Parse("2026-11-02T06:00:00Z"), UserId, "Me");
        MockPlannedTripService
            .GetEligibleAsync(Arg.Any<GetPlannedTripsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<PlannedTripOptionDto>>([option]));

        // Act
        var response = await Sut.Handle(new GetPlannedTripsQuery { UserId = UserId, WindowDays = 5 }, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Trips.Should().Equal(option);
        await MockPlannedTripService.Received(1).GetEligibleAsync(
            Arg.Is<GetPlannedTripsArgs>(args => args.UserId == UserId && args.WindowDays == 5),
            Arg.Any<CancellationToken>());
    }
}
