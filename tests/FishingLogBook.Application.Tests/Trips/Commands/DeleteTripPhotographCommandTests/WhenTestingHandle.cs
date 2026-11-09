using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Application.Trips.Contracts.Services;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Commands.DeleteTripPhotographCommandTests;

public class WhenTestingHandle
{
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SecondId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly ITripPhotographService _mockService = Substitute.For<ITripPhotographService>();
    private readonly DeleteTripPhotographHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new DeleteTripPhotographHandler(_mockService, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.DeleteAsync(Arg.Any<DeleteTripPhotographArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail("The service failed."));

        // Act
        var response = await _sut.Handle(
            new DeleteTripPhotographCommand { TripId = TripId, PhotographId = SecondId },
            CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        await _mockService.Received(1).DeleteAsync(
            Arg.Is<DeleteTripPhotographArgs>(args => args.TripId == TripId && args.PhotographId == SecondId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPassTheCommandToTheServiceAndReturnSuccess()
    {
        // Arrange
        _mockService.DeleteAsync(Arg.Any<DeleteTripPhotographArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var response = await _sut.Handle(
            new DeleteTripPhotographCommand { TripId = TripId, PhotographId = SecondId },
            CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        await _mockService.Received(1).DeleteAsync(
            Arg.Is<DeleteTripPhotographArgs>(args => args.TripId == TripId && args.PhotographId == SecondId),
            Arg.Any<CancellationToken>());
    }
}
