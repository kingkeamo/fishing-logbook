using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using Mapster;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Commands.UpdateCatchLocationVisibilityCommandTests;

public class WhenTestingHandle : BaseUpdateCatchLocationVisibilityCommandTest
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
            .UpdateLocationVisibilityAsync(Arg.Any<UpdateCatchLocationVisibilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail(new CatchNotOwnedError()));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.Error.Should().BeOfType<CatchNotOwnedError>();
        await MockCatchService.Received(1).UpdateLocationVisibilityAsync(
            Arg.Is<UpdateCatchLocationVisibilityArgs>(args =>
                args.CatchId == command.CatchId
                && args.Visibility == command.Visibility),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMapOnlyCatchIdAndVisibility()
    {
        // Arrange
        var command = ValidCommand();
        MockCatchService
            .UpdateLocationVisibilityAsync(Arg.Any<UpdateCatchLocationVisibilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        typeof(UpdateCatchLocationVisibilityCommand).GetProperty("ActorUserId").Should().BeNull();
        typeof(UpdateCatchLocationVisibilityCommand).GetProperty("OwnerUserId").Should().BeNull();
        typeof(UpdateCatchLocationVisibilityArgs).GetProperty("ActorUserId").Should().BeNull();
        await MockCatchService.Received(1).UpdateLocationVisibilityAsync(
            Arg.Is<UpdateCatchLocationVisibilityArgs>(args =>
                args.CatchId == command.CatchId
                && args.Visibility == LocationDefaults.Approximate),
            Arg.Any<CancellationToken>());
    }

    private static UpdateCatchLocationVisibilityCommand ValidCommand()
    {
        return new UpdateCatchLocationVisibilityCommand
        {
            CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Visibility = LocationDefaults.Approximate
        };
    }
}
