using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Application.Trips.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Queries.GetMyTripsQueryTests;

public class WhenTestingHandle
{
    private static readonly Guid Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly ITripService _mockService = Substitute.For<ITripService>();
    private readonly GetMyTripsHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new GetMyTripsHandler(_mockService, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.GetSummariesAsync(Arg.Any<GetMyTripsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<TripSummaryDto>>("The service failed."));

        // Act
        var response = await _sut.Handle(new GetMyTripsQuery { UserId = Id }, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Trips.Should().BeEmpty();
        await _mockService.Received(1).GetSummariesAsync(
            Arg.Is<GetMyTripsArgs>(args => args.UserId == Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheServiceResult()
    {
        // Arrange
        IReadOnlyList<TripSummaryDto> payload = new List<TripSummaryDto> { new(Id, "Active", DateTimeOffset.Parse("2026-09-12T06:30:00Z")) };
        _mockService.GetSummariesAsync(Arg.Any<GetMyTripsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(payload));

        // Act
        var response = await _sut.Handle(new GetMyTripsQuery { UserId = Id }, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Trips.Should().Equal(payload);
        await _mockService.Received(1).GetSummariesAsync(
            Arg.Is<GetMyTripsArgs>(args => args.UserId == Id),
            Arg.Any<CancellationToken>());
    }
}
