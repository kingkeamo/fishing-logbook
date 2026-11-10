using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Application.Trips.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Queries.GetTripQueryTests;

public class WhenTestingHandle
{
    private static readonly Guid Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly ITripDetailService _mockService = Substitute.For<ITripDetailService>();
    private readonly GetTripHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new GetTripHandler(_mockService, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.GetAsync(Arg.Any<GetTripArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripDetailDto>("The service failed."));

        // Act
        var response = await _sut.Handle(new GetTripQuery { TripId = Id }, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Trip.Should().BeNull();
        await _mockService.Received(1).GetAsync(
            Arg.Is<GetTripArgs>(args => args.TripId == Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheServiceResult()
    {
        // Arrange
        var payload = new TripDetailDto(new TripViewDto(Id, Guid.NewGuid(), "Active", DateTimeOffset.Parse("2026-09-12T06:30:00Z")));
        _mockService.GetAsync(Arg.Any<GetTripArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(payload));

        // Act
        var response = await _sut.Handle(new GetTripQuery { TripId = Id }, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Trip.Should().Be(payload);
        await _mockService.Received(1).GetAsync(
            Arg.Is<GetTripArgs>(args => args.TripId == Id),
            Arg.Any<CancellationToken>());
    }
}
