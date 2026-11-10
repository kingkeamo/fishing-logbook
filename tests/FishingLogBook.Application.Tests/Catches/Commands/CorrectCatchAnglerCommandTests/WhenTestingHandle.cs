using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Application.Catches.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Commands.CorrectCatchAnglerCommandTests;

public class WhenTestingHandle
{
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SecondId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly ICatchService _mockService = Substitute.For<ICatchService>();
    private readonly CorrectCatchAnglerHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new CorrectCatchAnglerHandler(_mockService, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.CorrectAnglerAsync(Arg.Any<CorrectCatchAnglerArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<CatchViewDto>("The service failed."));

        // Act
        var response = await _sut.Handle(
            new CorrectCatchAnglerCommand { CatchId = CatchId, CaughtByUserId = SecondId },
            CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Catch.Should().BeNull();
        await _mockService.Received(1).CorrectAnglerAsync(
            Arg.Is<CorrectCatchAnglerArgs>(args => args.CatchId == CatchId && args.CaughtByUserId == SecondId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPassTheCommandToTheServiceAndReturnSuccess()
    {
        // Arrange
        var payload = new CatchViewDto(CatchId, SecondId, DateTimeOffset.Parse("2026-09-12T06:30:00Z"));
        _mockService.CorrectAnglerAsync(Arg.Any<CorrectCatchAnglerArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(payload));

        // Act
        var response = await _sut.Handle(
            new CorrectCatchAnglerCommand { CatchId = CatchId, CaughtByUserId = SecondId },
            CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Catch.Should().Be(payload);
        await _mockService.Received(1).CorrectAnglerAsync(
            Arg.Is<CorrectCatchAnglerArgs>(args => args.CatchId == CatchId && args.CaughtByUserId == SecondId),
            Arg.Any<CancellationToken>());
    }
}
