using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Commands.ActivateTripCommandTests;

public class WhenTestingHandle
{
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly ITripService _mockTripService = Substitute.For<ITripService>();
    private readonly ActivateTripHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new ActivateTripHandler(_mockTripService, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockTripService.ActivateAsync(Arg.Any<ActivateTripArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripDto>(new TripTransitionInvalidError()));

        // Act
        var response = await _sut.Handle(new ActivateTripCommand { TripId = TripId }, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.Error.Should().BeOfType<TripTransitionInvalidError>();
        response.Trip.Should().BeNull();
        await _mockTripService.Received(1).ActivateAsync(
            Arg.Is<ActivateTripArgs>(args => args.TripId == TripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheActivatedTrip()
    {
        // Arrange
        var trip = new TripDto(TripId, TripConstants.Active, DateTimeOffset.Parse("2026-09-12T06:30:00Z"));
        _mockTripService.ActivateAsync(Arg.Any<ActivateTripArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(trip));

        // Act
        var response = await _sut.Handle(new ActivateTripCommand { TripId = TripId }, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Trip.Should().Be(trip);
    }
}
