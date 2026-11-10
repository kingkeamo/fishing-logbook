using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Commands.UpsertTripCommandTests;

public class WhenTestingHandle
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TripId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly TripDto Trip = new(
        TripId,
        TripConstants.Active,
        DateTimeOffset.Parse("2026-09-12T06:30:00Z"));

    private readonly ITripService _mockService = Substitute.For<ITripService>();
    private readonly UpsertTripHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new UpsertTripHandler(_mockService, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.UpsertAsync(Arg.Any<UpsertTripArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripDto>("The service failed."));

        // Act
        var response = await _sut.Handle(
            new UpsertTripCommand { UserId = UserId, Trip = Trip },
            CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Trip.Should().BeNull();
        await _mockService.Received(1).UpsertAsync(
            Arg.Is<UpsertTripArgs>(args => args.UserId == UserId && args.Trip == Trip),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheUpsertedTrip()
    {
        // Arrange
        _mockService.UpsertAsync(Arg.Any<UpsertTripArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(Trip));

        // Act
        var response = await _sut.Handle(
            new UpsertTripCommand { UserId = UserId, Trip = Trip },
            CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Trip.Should().Be(Trip);
        await _mockService.Received(1).UpsertAsync(
            Arg.Is<UpsertTripArgs>(args => args.UserId == UserId && args.Trip == Trip),
            Arg.Any<CancellationToken>());
    }
}
