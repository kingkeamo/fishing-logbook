using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using Mapster;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Commands.UpsertCatchCommandTests;

public class WhenTestingHandle : BaseUpsertCatchCommandTest
{
    public WhenTestingHandle()
    {
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        var command = ValidCommand();
        MockCatchService
            .UpsertAsync(Arg.Any<UpsertCatchArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<CatchDto>(new CatchHasNoPhotographsError()));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.Error.Should().BeOfType<CatchHasNoPhotographsError>();
        response.Catch.Should().BeNull();
        await MockCatchService.Received(1).UpsertAsync(
            Arg.Is<UpsertCatchArgs>(args =>
                args.UserId == command.UserId
                && args.Catch.Id == command.Catch.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheSavedCatch()
    {
        // Arrange
        var command = ValidCommand();
        var saved = command.Catch with { CaughtByUserId = command.UserId };
        MockCatchService
            .UpsertAsync(Arg.Any<UpsertCatchArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(saved));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Catch.Should().Be(saved);
        await MockCatchService.Received(1).UpsertAsync(
            Arg.Is<UpsertCatchArgs>(args =>
                args.UserId == command.UserId
                && args.Catch.Id == command.Catch.Id
                && args.Catch.Photographs[0].Id == command.Catch.Photographs[0].Id),
            Arg.Any<CancellationToken>());
    }

    private static UpsertCatchCommand ValidCommand()
    {
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var userId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        return new UpsertCatchCommand
        {
            UserId = userId,
            Catch = new CatchDto(
                catchId,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                [new CatchPhotographDto(photographId, catchId, PhotographContentTypeConstants.Jpeg)])
        };
    }
}
